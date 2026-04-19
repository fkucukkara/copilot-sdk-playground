using System.Text.Json;
using CopilotSDKPlayground.AgentsApi.Models;
using GitHub.Copilot.SDK;

namespace CopilotSDKPlayground.AgentsApi.Services;

/// <summary>
/// Pattern B — Orchestrator / Sub-agent parallel decomposition.
///
/// Architecture:
///   ┌─────────────┐  decomposes  ┌──────────┐  parallel  ┌──────────┐
///   │ Orchestrator│ ────────────▶│ SubTask 1│─────┐      │ SubTask N│
///   │   Agent     │              └──────────┘     │      └──────────┘
///   └─────────────┘              ┌──────────┐     ▼      ...
///          ▲                     │ SubTask 2│  Task.WhenAll
///          │ synthesizes         └──────────┘     │
///          └────────────────────────────────────────
///
/// The orchestrator decomposes a high-level goal into independent sub-tasks,
/// each assigned to a named agent. Sub-tasks run in parallel via <c>Task.WhenAll</c>.
/// Results are fed back to the orchestrator for synthesis into a final answer.
/// </summary>
public class OrchestratorAgentService : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly ILogger<OrchestratorAgentService> _logger;

    private static readonly Dictionary<string, string> AgentPrompts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Researcher"]  = "You are a research sub-agent. Provide factual, evidence-based information on the given topic.",
        ["Developer"]   = "You are a software developer sub-agent. Provide technical implementation details, code patterns, and best practices.",
        ["Analyst"]     = "You are an analysis sub-agent. Identify risks, tradeoffs, opportunities, and data-driven insights.",
        ["Writer"]      = "You are a writing sub-agent. Produce clear, concise, well-structured content suitable for the given audience.",
    };

    private const string DecompositionPrompt = """
        You are an orchestrator that decomposes goals into parallel sub-tasks.
        Available agents: Researcher, Developer, Analyst, Writer.

        Respond ONLY with a JSON array of 2-4 sub-tasks matching this schema:
        [
          {
            "id": "1",
            "description": "<specific sub-task description>",
            "assignedAgent": "<Researcher|Developer|Analyst|Writer>"
          }
        ]

        Rules:
        - Sub-tasks must be independent (no dependencies between them).
        - Each sub-task must be actionable by a single agent.
        - Do not include markdown fences or any text outside the JSON array.
        """;

    private const string SynthesisPrompt = """
        You are a synthesis orchestrator. You receive parallel sub-task results
        and combine them into a single, coherent, comprehensive response.
        Preserve key insights from each sub-task. Avoid repetition.
        """;

    public OrchestratorAgentService(
        IConfiguration configuration,
        ILogger<OrchestratorAgentService> logger)
    {
        _logger = logger;
        _client = new CopilotClient(new CopilotClientOptions
        {
            LogLevel = configuration.GetValue<string>("CopilotSDK:LogLevel", "info"),
            Logger = logger
        });
    }

    public async Task InitializeAsync() => await _client.StartAsync();

    /// <summary>
    /// Decomposes <paramref name="goal"/> into sub-tasks, executes them in parallel,
    /// then synthesises the results.
    /// </summary>
    public async Task<OrchestratorResponse> RunAsync(
        string goal,
        string model,
        CancellationToken cancellationToken = default)
    {
        // Step 1 — Decompose
        var subTasks = await DecomposeAsync(goal, model, cancellationToken);
        _logger.LogInformation("Decomposed '{Goal}' into {Count} sub-tasks", goal, subTasks.Count);

        // Step 2 — Execute sub-tasks in parallel
        var subTaskResults = await Task.WhenAll(
            subTasks.Select(t => ExecuteSubTaskAsync(t, model, cancellationToken)));

        // Step 3 — Synthesise
        var synthesized = await SynthesizeAsync(goal, subTaskResults, model, cancellationToken);

        return new OrchestratorResponse(
            Goal: goal,
            SubTaskResults: subTaskResults.ToList(),
            SynthesizedResponse: synthesized);
    }

    private async Task<List<SubTask>> DecomposeAsync(
        string goal,
        string model,
        CancellationToken cancellationToken)
    {
        var json = await InvokeEphemeralAsync(DecompositionPrompt, goal, model, cancellationToken);

        try
        {
            var tasks = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];
            return tasks.Select(el => new SubTask(
                Id: el.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                Description: el.GetProperty("description").GetString() ?? string.Empty,
                AssignedAgent: el.GetProperty("assignedAgent").GetString() ?? "Researcher"
            )).ToList();
        }
        catch (JsonException)
        {
            // Fallback: treat the whole goal as a single research task
            return [new SubTask("1", goal, "Researcher")];
        }
    }

    private async Task<SubTaskResult> ExecuteSubTaskAsync(
        SubTask task,
        string model,
        CancellationToken cancellationToken)
    {
        var agentPrompt = AgentPrompts.GetValueOrDefault(task.AssignedAgent, AgentPrompts["Researcher"]);
        _logger.LogInformation("Executing sub-task {Id} with {Agent}: {Desc}", task.Id, task.AssignedAgent, task.Description);

        var result = await InvokeEphemeralAsync(agentPrompt, task.Description, model, cancellationToken);
        return new SubTaskResult(task.Id, task.AssignedAgent, task.Description, result);
    }

    private async Task<string> SynthesizeAsync(
        string goal,
        IEnumerable<SubTaskResult> results,
        string model,
        CancellationToken cancellationToken)
    {
        var combinedResults = string.Join("\n\n---\n\n", results.Select((r, i) =>
            $"[Sub-task {r.SubTaskId} — {r.Agent}]\n{r.Description}\n\nResult:\n{r.Result}"));

        var synthesisInput = $"Original goal: {goal}\n\n{combinedResults}";

        return await InvokeEphemeralAsync(SynthesisPrompt, synthesisInput, model, cancellationToken);
    }

    private async Task<string> InvokeEphemeralAsync(
        string systemPrompt,
        string userMessage,
        string model,
        CancellationToken cancellationToken)
    {
        await using var session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            }
        });

        var response = string.Empty;
        var tcs = new TaskCompletionSource<string>();
        using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        var subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    response = msg.Data.Content ?? string.Empty;
                    break;
                case SessionIdleEvent:
                    tcs.TrySetResult(response);
                    break;
                case SessionErrorEvent err:
                    tcs.TrySetException(new InvalidOperationException(err.Data.Message));
                    break;
            }
        });

        try
        {
            await session.SendAsync(new MessageOptions { Prompt = userMessage });
            return await tcs.Task;
        }
        finally
        {
            subscription.Dispose();
        }
    }

    public async ValueTask DisposeAsync() => await _client.StopAsync();
}

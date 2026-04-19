using System.Text.Json;
using GitHub.Copilot.SDK;

namespace CopilotSDKPlayground.AgentsApi.Services;

/// <summary>
/// Pattern A — Coordinator / Specialist multi-agent orchestration.
///
/// Architecture:
///   ┌─────────────┐     routes to     ┌──────────────────┐
///   │ Coordinator │ ─────────────────▶ │ Specialist Agent │
///   │   Agent     │                   │ (Research / Code /│
///   └─────────────┘                   │  Writing / Data)  │
///                                     └──────────────────┘
///
/// The Coordinator receives the user's task and decides which specialist
/// is best suited to handle it. It returns its reasoning AND the specialist's
/// full response. Each agent is a separate <see cref="CopilotSession"/> with
/// its own system prompt.
/// </summary>
public class CoordinatorAgentService : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly ILogger<CoordinatorAgentService> _logger;

    // Specialist system prompts — each models a domain expert
    private static readonly Dictionary<string, string> Specialists = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Research"] =
            "You are a research specialist. You find accurate information, cite sources, " +
            "and provide well-structured summaries. Focus on facts and evidence.",

        ["Code"] =
            "You are a senior software engineer. You write clean, idiomatic code with " +
            "explanations. Include edge cases and follow best practices. Use markdown code blocks.",

        ["Writing"] =
            "You are a professional technical writer. You produce clear, concise, " +
            "well-structured prose. Adapt tone to context: formal for docs, friendly for blogs.",

        ["Data"] =
            "You are a data analyst. You interpret data, identify trends, suggest " +
            "visualisations, and provide actionable insights with statistical rigour."
    };

    private const string CoordinatorSystemPrompt = """
        You are a coordinator that routes tasks to the best specialist agent.
        Available specialists: Research, Code, Writing, Data.

        Respond ONLY with valid JSON matching this schema:
        {
          "agent": "<Research|Code|Writing|Data>",
          "reasoning": "<one sentence explaining why this specialist fits best>"
        }

        Do not include markdown fences or any text outside the JSON object.
        """;

    public CoordinatorAgentService(
        IConfiguration configuration,
        ILogger<CoordinatorAgentService> logger)
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
    /// Routes <paramref name="task"/> through the coordinator, then executes it
    /// on the selected specialist agent.
    /// </summary>
    public async Task<(string AgentUsed, string Reasoning, string Response)> RunAsync(
        string task,
        string model,
        CancellationToken cancellationToken = default)
    {
        // Step 1 — Ask the coordinator which specialist to use
        var (agentName, reasoning) = await RouteAsync(task, model, cancellationToken);
        _logger.LogInformation("Coordinator selected '{Agent}': {Reasoning}", agentName, reasoning);

        // Step 2 — Execute the task on the selected specialist
        var response = await ExecuteWithSpecialistAsync(task, agentName, model, cancellationToken);

        return (agentName, reasoning, response);
    }

    private async Task<(string AgentName, string Reasoning)> RouteAsync(
        string task,
        string model,
        CancellationToken cancellationToken)
    {
        var json = await InvokeEphemeralAsync(
            systemPrompt: CoordinatorSystemPrompt,
            userMessage: task,
            model: model,
            cancellationToken: cancellationToken);

        try
        {
            var doc = JsonDocument.Parse(json);
            var agentName = doc.RootElement.GetProperty("agent").GetString() ?? "Research";
            var reasoning = doc.RootElement.GetProperty("reasoning").GetString() ?? string.Empty;

            // Validate — fall back to Research if unknown
            return Specialists.ContainsKey(agentName)
                ? (agentName, reasoning)
                : ("Research", $"Defaulted to Research (coordinator returned '{agentName}')");
        }
        catch (JsonException)
        {
            return ("Research", "Defaulted to Research (coordinator returned invalid JSON)");
        }
    }

    private Task<string> ExecuteWithSpecialistAsync(
        string task,
        string agentName,
        string model,
        CancellationToken cancellationToken)
    {
        var systemPrompt = Specialists[agentName];
        return InvokeEphemeralAsync(systemPrompt, task, model, cancellationToken);
    }

    /// <summary>
    /// Creates a one-shot session, sends a single message, returns the response, then disposes.
    /// This models an ephemeral agent — no session state carried between calls.
    /// </summary>
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

using CopilotSDKPlayground.AgentsApi.Models;
using CopilotSDKPlayground.AgentsApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CopilotSDKPlayground.AgentsApi.Endpoints;

/// <summary>
/// Multi-agent orchestration endpoints — two distinct patterns:
///
/// Pattern A — Coordinator (/api/agents/coordinator):
///   Single request → coordinator routes → specialist responds.
///   Best for: single-turn tasks where the domain is unknown upfront.
///
/// Pattern B — Orchestrator (/api/agents/orchestrator):
///   Single request → decompose → parallel sub-agents → synthesise.
///   Best for: complex goals that benefit from parallel specialisation.
/// </summary>
public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agents").WithTags("Multi-Agent Orchestration");

        // Pattern A
        group.MapPost("/coordinator", RunCoordinator)
             .WithSummary("Coordinator pattern — routes the task to the best specialist agent");

        // Pattern B
        group.MapPost("/orchestrator", RunOrchestrator)
             .WithSummary("Orchestrator pattern — decomposes into parallel sub-tasks then synthesises");

        return app;
    }

    // ── Pattern A ─────────────────────────────────────────────────────────────

    private static async Task<Ok<CoordinatorResponse>> RunCoordinator(
        AgentRequest request,
        CoordinatorAgentService service,
        IConfiguration configuration,
        HttpContext context)
    {
        var model = request.Model ?? configuration.GetValue<string>("CopilotSDK:DefaultModel", "gpt-4o")!;
        var (agentUsed, reasoning, response) = await service.RunAsync(request.Task, model, context.RequestAborted);

        return TypedResults.Ok(new CoordinatorResponse(
            AgentUsed: agentUsed,
            Reasoning: reasoning,
            Response: response));
    }

    // ── Pattern B ─────────────────────────────────────────────────────────────

    private static async Task<Ok<OrchestratorResponse>> RunOrchestrator(
        OrchestratorRequest request,
        OrchestratorAgentService service,
        IConfiguration configuration,
        HttpContext context)
    {
        var model = request.Model ?? configuration.GetValue<string>("CopilotSDK:DefaultModel", "gpt-4o")!;
        var result = await service.RunAsync(request.Goal, model, context.RequestAborted);
        return TypedResults.Ok(result);
    }
}

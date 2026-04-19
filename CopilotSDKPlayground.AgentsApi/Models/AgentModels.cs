namespace CopilotSDKPlayground.AgentsApi.Models;

// ── Shared ─────────────────────────────────────────────────────────────────────

/// <summary>Input for any agent-based request.</summary>
public record AgentRequest(string Task, string? Model = null);

// ── Pattern A: Coordinator / Specialist agents ─────────────────────────────────

/// <summary>
/// Response from the Coordinator pattern.
/// The coordinator analyses the task, selects the best specialist, and returns
/// both the specialist's response and the coordinator's reasoning.
/// </summary>
public record CoordinatorResponse(
    string AgentUsed,
    string Reasoning,
    string Response);

// ── Pattern B: Orchestrator / Sub-agent parallel decomposition ─────────────────

/// <summary>Request body for the Orchestrator pattern.</summary>
public record OrchestratorRequest(string Goal, string? Model = null);

/// <summary>A single sub-task produced by the orchestrator.</summary>
public record SubTask(string Id, string Description, string AssignedAgent);

/// <summary>Result of a single sub-task execution.</summary>
public record SubTaskResult(string SubTaskId, string Agent, string Description, string Result);

/// <summary>
/// Response from the Orchestrator pattern.
/// The orchestrator decomposes the goal into sub-tasks, runs them in parallel,
/// and then synthesises the results into a final answer.
/// </summary>
public record OrchestratorResponse(
    string Goal,
    IReadOnlyList<SubTaskResult> SubTaskResults,
    string SynthesizedResponse);

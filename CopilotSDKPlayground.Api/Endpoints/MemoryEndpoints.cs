using CopilotSDKPlayground.Api.Models;
using CopilotSDKPlayground.Api.Services;
using GitHub.Copilot.SDK;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CopilotSDKPlayground.Api.Endpoints;

/// <summary>
/// Demonstrates Redis-backed memory persistence across Copilot sessions.
/// Prior conversation summaries are injected as context when a new session starts.
/// After <c>MaxTurnsBeforeCompaction</c> turns the session is summarised and the digest saved.
/// </summary>
public static class MemoryEndpoints
{
    public static IEndpointRouteBuilder MapMemoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/memory").WithTags("Memory");

        group.MapPost("/sessions", CreateMemorySession)
             .WithSummary("Create a session that loads prior context from Redis");

        group.MapPost("/{sessionId}/messages", SendMemoryMessage)
             .WithSummary("Send a message; auto-compacts to Redis when turn limit is reached");

        group.MapGet("/{userId}", GetUserMemory)
             .WithSummary("Retrieve stored memory summary for a user");

        group.MapDelete("/{userId}", DeleteUserMemory)
             .WithSummary("Clear stored memory for a user");

        return app;
    }

    private static async Task<Created<MemorySessionResponse>> CreateMemorySession(
        MemorySessionRequest request,
        ICopilotService copilotService,
        MemoryService memoryService,
        IConfiguration configuration)
    {
        var model = request.Model ?? configuration.GetValue<string>("CopilotSDK:DefaultModel", "gpt-4o")!;
        var priorContext = await memoryService.GetMemoryAsync(request.UserId);

        SessionConfig config = priorContext is not null
            ? new SessionConfig
            {
                Model = model,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Append,
                    Content =
                        $"<memory>\nPrevious conversation summary for this user:\n{priorContext}\n</memory>\n" +
                        "Use this context to maintain continuity across sessions."
                }
            }
            : new SessionConfig { Model = model, OnPermissionRequest = PermissionHandler.ApproveAll };

        var session = await copilotService.CreateSessionAsync(config);

        return TypedResults.Created($"/api/chat/{session.SessionId}", new MemorySessionResponse
        {
            SessionId = session.SessionId,
            UserId = request.UserId,
            ContextLoaded = priorContext is not null,
            ContextSummary = priorContext is not null
                ? priorContext[..Math.Min(200, priorContext.Length)] + "…"
                : null
        });
    }

    private static async Task<Results<Ok<ChatMessageResponse>, NotFound>> SendMemoryMessage(
        string sessionId,
        ChatMessageRequest request,
        ICopilotService copilotService,
        MemoryService memoryService,
        HttpContext context)
    {
        // userId is passed as a query parameter so the memory service knows whose context to update
        var userId = context.Request.Query["userId"].FirstOrDefault();
        var session = await copilotService.GetSessionAsync(sessionId);
        if (session is null)
            return TypedResults.NotFound();

        var content = await copilotService.SendMessageAsync(session, request.Prompt, context.RequestAborted);

        // Fire-and-forget compaction (non-blocking)
        if (!string.IsNullOrWhiteSpace(userId))
            _ = memoryService.MaybeCompactAsync(userId, session);

        return TypedResults.Ok(new ChatMessageResponse { Content = content });
    }

    private static async Task<Results<Ok<UserMemoryResponse>, NotFound>> GetUserMemory(
        string userId,
        MemoryService memoryService)
    {
        var summary = await memoryService.GetMemoryAsync(userId);
        return TypedResults.Ok(new UserMemoryResponse
        {
            UserId = userId,
            Summary = summary,
            HasMemory = summary is not null,
            LastUpdatedAt = summary is not null ? await memoryService.GetLastUpdatedAsync(userId) : null
        });
    }

    private static async Task<NoContent> DeleteUserMemory(
        string userId,
        MemoryService memoryService)
    {
        await memoryService.DeleteUserMemoryAsync(userId);
        return TypedResults.NoContent();
    }
}

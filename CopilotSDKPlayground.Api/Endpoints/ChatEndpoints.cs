using CopilotSDKPlayground.Api.Models;
using CopilotSDKPlayground.Api.Services;
using GitHub.Copilot.SDK;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CopilotSDKPlayground.Api.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat").WithTags("Chat");

        group.MapPost("/sessions", CreateSession).WithSummary("Create a new chat session");
        group.MapPost("/{sessionId}/messages", SendMessage).WithSummary("Send a message");
        group.MapGet("/{sessionId}/history", GetSessionHistory).WithSummary("Get session history");
        group.MapDelete("/{sessionId}", DeleteSession).WithSummary("Delete a session");

        return app;
    }

    private static async Task<Created<CreateSessionResponse>> CreateSession(
        CreateSessionRequest request,
        ICopilotService copilotService,
        IConfiguration configuration)
    {
        var model = request.Model ?? configuration.GetValue<string>("CopilotSDK:DefaultModel", "gpt-4o")!;

        var session = await copilotService.CreateSessionAsync(model, request.SessionId);

        var response = new CreateSessionResponse { SessionId = session.SessionId, Model = model };

        return TypedResults.Created($"/api/chat/{session.SessionId}", response);
    }

    private static async Task<Results<Ok<ChatMessageResponse>, NotFound>> SendMessage(
        string sessionId,
        ChatMessageRequest request,
        ICopilotService copilotService,
        HttpContext context)
    {
        var session = await copilotService.GetSessionAsync(sessionId);
        if (session is null)
        {
            return TypedResults.NotFound();
        }

        var content = await copilotService.SendMessageAsync(session, request.Prompt, context.RequestAborted);
        return TypedResults.Ok(new ChatMessageResponse { Content = content });
    }

    private static async Task<Results<Ok<SessionHistoryResponse>, NotFound>> GetSessionHistory(
        string sessionId,
        ICopilotService copilotService)
    {
        var session = await copilotService.GetSessionAsync(sessionId);
        if (session is null)
        {
            return TypedResults.NotFound();
        }

        var messages = await copilotService.GetSessionMessagesAsync(session);
        var events = messages.Select(m => new MessageEvent
        {
            Type = m.GetType().Name,
            Content = m switch
            {
                UserMessageEvent u => u.Data.Content ?? string.Empty,
                AssistantMessageEvent a => a.Data.Content ?? string.Empty,
                _ => m.GetType().Name
            }
        }).ToList();

        return TypedResults.Ok(new SessionHistoryResponse { SessionId = sessionId, Messages = events });
    }

    private static async Task<Results<NoContent, NotFound>> DeleteSession(
        string sessionId,
        ICopilotService copilotService)
    {
        var deleted = await copilotService.DeleteSessionAsync(sessionId);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}

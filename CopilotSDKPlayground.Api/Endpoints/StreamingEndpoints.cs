using CopilotSDKPlayground.Api.Services;

namespace CopilotSDKPlayground.Api.Endpoints;

/// <summary>
/// Demonstrates real-time token streaming via Server-Sent Events (SSE).
/// Sessions are created with <c>Streaming = true</c> so the Copilot SDK emits
/// <c>AssistantMessageDeltaEvent</c> per token chunk; each chunk is forwarded
/// to the HTTP response as an SSE <c>data:</c> frame.
/// </summary>
public static class StreamingEndpoints
{
    public static IEndpointRouteBuilder MapStreamingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stream").WithTags("Streaming");

        group.MapPost("/sessions", CreateStreamingSession)
             .WithSummary("Create a streaming-enabled chat session (Streaming=true)");

        group.MapGet("/{sessionId}", StreamMessage)
             .WithSummary("Stream a Copilot response as Server-Sent Events")
             .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
             .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    // POST /api/stream/sessions
    private static async Task<IResult> CreateStreamingSession(
        IStreamingCopilotService streamingService,
        IConfiguration configuration,
        string? model = null,
        string? sessionId = null)
    {
        var resolvedModel = model ?? configuration.GetValue<string>("CopilotSDK:DefaultModel", "gpt-4o")!;
        var session = await streamingService.CreateStreamingSessionAsync(resolvedModel, sessionId);
        return Results.Created($"/api/stream/{session.SessionId}", new
        {
            session.SessionId,
            Model = resolvedModel,
            Streaming = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    // GET /api/stream/{sessionId}?prompt=Hello
    private static async Task StreamMessage(
        string sessionId,
        string prompt,
        ICopilotService copilotService,
        IStreamingCopilotService streamingService,
        HttpContext context)
    {
        var session = await copilotService.GetSessionAsync(sessionId);
        if (session is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        await foreach (var chunk in streamingService.StreamMessageAsync(session, prompt, context.RequestAborted))
        {
            // SSE format: each token chunk as a data frame
            await context.Response.WriteAsync($"data: {chunk}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }

        // Signal stream end
        await context.Response.WriteAsync("data: [DONE]\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
}

using CopilotSDKPlayground.RagApi.Models;
using CopilotSDKPlayground.RagApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CopilotSDKPlayground.RagApi.Endpoints;

/// <summary>
/// RAG chat endpoints — create sessions, query the knowledge base with LLM synthesis.
/// Each query: retrieve top-K chunks → build augmented prompt → Copilot generates answer.
/// The response includes <see cref="RagQueryResponse.RetrievedChunks"/> so you can
/// inspect exactly what context was injected.
/// </summary>
public static class RagChatEndpoints
{
    public static IEndpointRouteBuilder MapRagChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rag").WithTags("RAG - Chat");

        group.MapPost("/sessions", CreateRagSession)
             .WithSummary("Create a RAG-enabled chat session");

        group.MapPost("/{sessionId}/query", Query)
             .WithSummary("Query the knowledge base — retrieves context then generates an answer");

        group.MapDelete("/{sessionId}", DeleteSession)
             .WithSummary("Delete a RAG session");

        return app;
    }

    private static async Task<Created<RagSessionResponse>> CreateRagSession(
        RagSessionRequest request,
        RagCopilotService ragService,
        IConfiguration configuration)
    {
        var model = request.Model ?? configuration.GetValue<string>("CopilotSDK:DefaultModel", "gpt-4o")!;
        var (sessionId, topK) = await ragService.CreateSessionAsync(model, request.TopK, request.SessionId);

        return TypedResults.Created($"/api/rag/{sessionId}", new RagSessionResponse
        {
            SessionId = sessionId,
            TopK = topK
        });
    }

    private static async Task<Results<Ok<RagQueryResponse>, NotFound>> Query(
        string sessionId,
        RagQueryRequest request,
        RagCopilotService ragService,
        HttpContext context)
    {
        if (!ragService.SessionExists(sessionId))
            return TypedResults.NotFound();

        var (answer, chunks) = await ragService.QueryAsync(sessionId, request.Query, context.RequestAborted);

        return TypedResults.Ok(new RagQueryResponse
        {
            Answer = answer,
            RetrievedChunks = chunks.ToList()
        });
    }

    private static async Task<Results<NoContent, NotFound>> DeleteSession(
        string sessionId,
        RagCopilotService ragService)
    {
        return await ragService.DeleteSessionAsync(sessionId)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }
}

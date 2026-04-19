using CopilotSDKPlayground.RagApi.Models;
using CopilotSDKPlayground.RagApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CopilotSDKPlayground.RagApi.Endpoints;

/// <summary>
/// Document management endpoints — ingest, list, delete knowledge base documents.
/// Each document is chunked and stored in the <see cref="InMemoryVectorStore"/>.
/// </summary>
public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rag/documents").WithTags("RAG - Documents");

        group.MapGet("/", ListDocuments).WithSummary("List all ingested documents");
        group.MapPost("/", IngestDocument).WithSummary("Ingest a document into the knowledge base");
        group.MapDelete("/{documentId}", DeleteDocument).WithSummary("Remove a document from the knowledge base");

        return app;
    }

    private static Ok<List<DocumentSummary>> ListDocuments(InMemoryVectorStore store) =>
        TypedResults.Ok(store.ListDocuments().ToList());

    private static Ok<IngestDocumentResponse> IngestDocument(
        IngestDocumentRequest request,
        InMemoryVectorStore store)
    {
        var result = store.Ingest(request.Title, request.Content, request.Source);
        return TypedResults.Ok(result);
    }

    private static Results<NoContent, NotFound> DeleteDocument(
        string documentId,
        InMemoryVectorStore store)
    {
        return store.DeleteDocument(documentId)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }
}

namespace CopilotSDKPlayground.RagApi.Models;

// ── Document Ingestion ────────────────────────────────────────────────────────

public class IngestDocumentRequest
{
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string? Source { get; set; }
}

public class IngestDocumentResponse
{
    public required string DocumentId { get; set; }
    public required string Title { get; set; }
    public int ChunkCount { get; set; }
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;
}

public class DocumentSummary
{
    public required string DocumentId { get; set; }
    public required string Title { get; set; }
    public string? Source { get; set; }
    public int ChunkCount { get; set; }
    public DateTime IngestedAt { get; set; }
}

// ── RAG Chat ──────────────────────────────────────────────────────────────────

public class RagSessionRequest
{
    public string? Model { get; set; }
    public string? SessionId { get; set; }

    /// <summary>Number of top-K chunks to inject as context (default: 3).</summary>
    public int TopK { get; set; } = 3;
}

public class RagSessionResponse
{
    public required string SessionId { get; set; }
    public int TopK { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RagQueryRequest
{
    public required string Query { get; set; }
}

public class RagQueryResponse
{
    public required string Answer { get; set; }
    public required List<RetrievedChunk> RetrievedChunks { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class RetrievedChunk
{
    public required string DocumentId { get; set; }
    public required string DocumentTitle { get; set; }
    public required string Content { get; set; }
    public double SimilarityScore { get; set; }
    public int ChunkIndex { get; set; }
}

using System.Collections.Concurrent;
using CopilotSDKPlayground.RagApi.Models;

namespace CopilotSDKPlayground.RagApi.Services;

/// <summary>
/// Pure C# in-memory vector store using TF-IDF vectors and cosine similarity.
/// No external dependencies — ideal for educational demos.
/// Thread-safe via <see cref="ConcurrentDictionary"/>.
/// </summary>
public class InMemoryVectorStore
{
    private record VectorEntry(
        string DocumentId,
        string DocumentTitle,
        string Content,
        int ChunkIndex,
        Dictionary<string, double> TfIdfVector);

    private readonly ConcurrentDictionary<string, VectorEntry> _chunks = new();
    private readonly ConcurrentDictionary<string, DocumentSummary> _documents = new();

    // ── Document ingestion ────────────────────────────────────────────────────

    public IngestDocumentResponse Ingest(string title, string content, string? source = null)
    {
        var documentId = Guid.NewGuid().ToString("N")[..8];
        var chunks = DocumentChunker.Chunk(content);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkId = $"{documentId}_{i}";
            var vector = ComputeTfIdf(chunks[i]);
            _chunks[chunkId] = new VectorEntry(documentId, title, chunks[i], i, vector);
        }

        var summary = new DocumentSummary
        {
            DocumentId = documentId,
            Title = title,
            Source = source,
            ChunkCount = chunks.Count,
            IngestedAt = DateTime.UtcNow
        };
        _documents[documentId] = summary;

        return new IngestDocumentResponse
        {
            DocumentId = documentId,
            Title = title,
            ChunkCount = chunks.Count
        };
    }

    public IReadOnlyList<DocumentSummary> ListDocuments() =>
        _documents.Values.OrderByDescending(d => d.IngestedAt).ToList();

    public bool DeleteDocument(string documentId)
    {
        if (!_documents.TryRemove(documentId, out _)) return false;
        var keys = _chunks.Keys.Where(k => k.StartsWith(documentId + "_")).ToList();
        foreach (var key in keys) _chunks.TryRemove(key, out _);
        return true;
    }

    // ── Retrieval ─────────────────────────────────────────────────────────────

    public IReadOnlyList<RetrievedChunk> Search(string query, int topK = 3)
    {
        if (_chunks.IsEmpty) return [];

        var queryVector = ComputeTfIdf(query);

        return _chunks.Values
            .Select(entry => new
            {
                Entry = entry,
                Score = CosineSimilarity(queryVector, entry.TfIdfVector)
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new RetrievedChunk
            {
                DocumentId = x.Entry.DocumentId,
                DocumentTitle = x.Entry.DocumentTitle,
                Content = x.Entry.Content,
                ChunkIndex = x.Entry.ChunkIndex,
                SimilarityScore = Math.Round(x.Score, 4)
            })
            .ToList();
    }

    // ── TF-IDF computation ────────────────────────────────────────────────────

    private static Dictionary<string, double> ComputeTfIdf(string text)
    {
        var tokens = Tokenise(text);
        if (tokens.Length == 0) return [];

        // Term frequency
        var tf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
            tf[token] = tf.GetValueOrDefault(token) + 1.0 / tokens.Length;

        // Use log(1 + tf) as a simple IDF proxy (no corpus-wide IDF for demo simplicity)
        return tf.ToDictionary(
            kv => kv.Key,
            kv => Math.Log(1 + kv.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string[] Tokenise(string text) =>
        System.Text.RegularExpressions.Regex
            .Replace(text.ToLowerInvariant(), @"[^a-z0-9\s]", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2 && !StopWords.Contains(t))
            .ToArray();

    private static double CosineSimilarity(
        Dictionary<string, double> a,
        Dictionary<string, double> b)
    {
        var dotProduct = 0.0;
        var magnitudeA = 0.0;
        var magnitudeB = 0.0;

        foreach (var (term, weightA) in a)
        {
            magnitudeA += weightA * weightA;
            if (b.TryGetValue(term, out var weightB))
                dotProduct += weightA * weightB;
        }

        foreach (var (_, weightB) in b)
            magnitudeB += weightB * weightB;

        var denominator = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","and","for","are","but","not","you","all","can","had","her","was","one",
        "our","out","day","get","has","him","his","how","man","new","now","old","see",
        "two","way","who","did","its","let","put","say","she","too","use","that","this",
        "with","they","have","from","been","when","were","will","each","than","then",
        "into","some","what","them","your","more","also","about","would","there","their",
        "these","which","should","could","other","after","first","before","where","while"
    };
}

using System.Collections.Concurrent;
using GitHub.Copilot.SDK;

namespace CopilotSDKPlayground.RagApi.Services;

/// <summary>
/// Wraps the Copilot SDK to add retrieval-augmented generation.
/// On each query: retrieve top-K chunks → inject as context → send to Copilot.
/// Pattern is identical to <c>CopilotService</c> in the Api project for consistency.
/// </summary>
public class RagCopilotService : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly InMemoryVectorStore _vectorStore;
    private readonly ILogger<RagCopilotService> _logger;
    private readonly ConcurrentDictionary<string, (CopilotSession Session, int TopK)> _sessions = new();

    public RagCopilotService(
        InMemoryVectorStore vectorStore,
        IConfiguration configuration,
        ILogger<RagCopilotService> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
        _client = new CopilotClient(new CopilotClientOptions
        {
            LogLevel = configuration.GetValue<string>("CopilotSDK:LogLevel", "info"),
            Logger = logger
        });
    }

    public async Task InitializeAsync()
    {
        await _client.StartAsync();
        _logger.LogInformation("RAG Copilot client started");
    }

    public async Task<(string SessionId, int TopK)> CreateSessionAsync(
        string model,
        int topK = 3,
        string? sessionId = null)
    {
        var session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SessionId = sessionId,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content =
                    "You are a precise assistant that answers questions strictly based on the provided context. " +
                    "If the context does not contain enough information to answer, say so clearly. " +
                    "Always cite which document the information came from."
            }
        });

        _sessions[session.SessionId] = (session, topK);
        _logger.LogInformation("Created RAG session {SessionId} topK={TopK}", session.SessionId, topK);
        return (session.SessionId, topK);
    }

    public bool SessionExists(string sessionId) => _sessions.ContainsKey(sessionId);

    /// <summary>
    /// Retrieves relevant chunks, builds an augmented prompt, and sends it to Copilot.
    /// Returns the answer and the chunks used for context.
    /// </summary>
    public async Task<(string Answer, IReadOnlyList<Models.RetrievedChunk> Chunks)> QueryAsync(
        string sessionId,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
            throw new KeyNotFoundException($"RAG session '{sessionId}' not found.");

        var chunks = _vectorStore.Search(query, entry.TopK);

        var augmentedPrompt = chunks.Count > 0
            ? BuildAugmentedPrompt(query, chunks)
            : $"[No documents in the knowledge base yet]\n\nQuestion: {query}";

        var answer = await SendMessageAsync(entry.Session, augmentedPrompt, cancellationToken);
        return (answer, chunks);
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var entry)) return false;
        await entry.Session.DisposeAsync();
        await _client.DeleteSessionAsync(sessionId);
        return true;
    }

    private static string BuildAugmentedPrompt(
        string query,
        IReadOnlyList<Models.RetrievedChunk> chunks)
    {
        var context = string.Join("\n\n---\n\n", chunks.Select((c, i) =>
            $"[Source {i + 1}: {c.DocumentTitle} | similarity={c.SimilarityScore:F3}]\n{c.Content}"));

        return $"Context:\n{context}\n\n---\n\nQuestion: {query}";
    }

    private async Task<string> SendMessageAsync(
        CopilotSession session,
        string prompt,
        CancellationToken cancellationToken)
    {
        var responseContent = string.Empty;
        var tcs = new TaskCompletionSource<string>();
        using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        var subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    responseContent = msg.Data.Content ?? string.Empty;
                    break;
                case SessionIdleEvent:
                    tcs.TrySetResult(responseContent);
                    break;
                case SessionErrorEvent err:
                    tcs.TrySetException(new InvalidOperationException(err.Data.Message));
                    break;
            }
        });

        try
        {
            await session.SendAsync(new MessageOptions { Prompt = prompt });
            return await tcs.Task;
        }
        finally
        {
            subscription.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (session, _) in _sessions.Values)
            await session.DisposeAsync();
        _sessions.Clear();
        await _client.StopAsync();
    }
}

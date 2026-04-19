using GitHub.Copilot.SDK;

namespace CopilotSDKPlayground.Api.Services;

/// <summary>
/// Extends the base Copilot service with streaming (SSE) message support.
/// Sessions created here have <c>Streaming = true</c>, enabling
/// <see cref="AssistantMessageDeltaEvent"/> token chunks.
/// </summary>
public interface IStreamingCopilotService
{
    Task<CopilotSession> CreateStreamingSessionAsync(string model, string? sessionId = null);

    IAsyncEnumerable<string> StreamMessageAsync(
        CopilotSession session,
        string prompt,
        CancellationToken cancellationToken = default);
}

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GitHub.Copilot.SDK;

namespace CopilotSDKPlayground.Api.Services;

/// <summary>
/// Provides token-by-token streaming of Copilot responses using SSE-compatible async enumerable.
/// Sessions must be created with <c>Streaming = true</c> in their <see cref="SessionConfig"/> so
/// the SDK emits <see cref="AssistantMessageDeltaEvent"/> chunks during generation.
/// </summary>
public class StreamingCopilotService : IStreamingCopilotService
{
    private readonly ICopilotService _copilotService;
    private readonly ILogger<StreamingCopilotService> _logger;

    public StreamingCopilotService(
        ICopilotService copilotService,
        ILogger<StreamingCopilotService> logger)
    {
        _copilotService = copilotService;
        _logger = logger;
    }

    /// <summary>Creates a streaming-enabled session (Streaming = true).</summary>
    public Task<CopilotSession> CreateStreamingSessionAsync(string model, string? sessionId = null) =>
        _copilotService.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SessionId = sessionId,
            Streaming = true,
            OnPermissionRequest = PermissionHandler.ApproveAll
        });

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamMessageAsync(
        CopilotSession session,
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Unbounded channel: SDK event handler writes, HTTP response reader consumes
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

        var subscription = session.On(evt =>
        {
            switch (evt)
            {
                // Delta chunks arrive only when Streaming = true on the session
                case AssistantMessageDeltaEvent delta when !string.IsNullOrEmpty(delta.Data.DeltaContent):
                    channel.Writer.TryWrite(delta.Data.DeltaContent);
                    break;
                case SessionIdleEvent:
                    channel.Writer.TryComplete();
                    break;
                case SessionErrorEvent err:
                    channel.Writer.TryComplete(new InvalidOperationException(err.Data.Message));
                    break;
            }
        });

        try
        {
            await session.SendAsync(new MessageOptions { Prompt = prompt });

            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return chunk;
            }
        }
        finally
        {
            subscription.Dispose();
        }
    }
}

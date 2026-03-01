using System.Collections.Concurrent;
using GitHub.Copilot.SDK;

namespace CopilotSDKPlayground.Api.Services;

public class CopilotService : ICopilotService, IAsyncDisposable
{
    private readonly CopilotClient _client;
    private readonly ILogger<CopilotService> _logger;
    private readonly ConcurrentDictionary<string, CopilotSession> _sessions = new();

    public CopilotService(IConfiguration configuration, ILogger<CopilotService> logger)
    {
        _logger = logger;
        _client = new CopilotClient(new CopilotClientOptions
        {
            LogLevel = configuration.GetValue<string>("CopilotSDK:LogLevel", "info"),
            Logger = logger
        });
    }

    /// <summary>
    /// Initializes the Copilot client connection.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _client.StartAsync();
            _logger.LogInformation("Copilot SDK client started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Copilot SDK client");
            throw;
        }
    }

    public async Task<CopilotSession> CreateSessionAsync(string model, string? sessionId = null)
    {
        var session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SessionId = sessionId,
            OnPermissionRequest = PermissionHandler.ApproveAll
        });

        _sessions.TryAdd(session.SessionId, session);
        _logger.LogInformation("Created session {SessionId} with model {Model}", session.SessionId, model);

        return session;
    }

    public async Task<string> SendMessageAsync(
        CopilotSession session,
        string prompt,
        CancellationToken cancellationToken = default)
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

    public Task<IReadOnlyList<SessionEvent>> GetSessionMessagesAsync(CopilotSession session) =>
        session.GetMessagesAsync();

    public Task<CopilotSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
            return false;

        await session.DisposeAsync();
        await _client.DeleteSessionAsync(sessionId);
        _logger.LogInformation("Deleted session {SessionId}", sessionId);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
            await session.DisposeAsync();
        _sessions.Clear();
        await _client.StopAsync();
    }
}

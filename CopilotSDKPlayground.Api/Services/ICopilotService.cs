using GitHub.Copilot.SDK;

namespace CopilotSDKPlayground.Api.Services;

public interface ICopilotService
{
    Task InitializeAsync();

    /// <summary>Convenience overload — creates a session with default SDK config.</summary>
    Task<CopilotSession> CreateSessionAsync(string model, string? sessionId = null);

    /// <summary>
    /// Full-control overload that accepts a pre-built <see cref="SessionConfig"/>.
    /// Use this to register tools, customise the system message, enable streaming, etc.
    /// </summary>
    Task<CopilotSession> CreateSessionAsync(SessionConfig config);

    Task<string> SendMessageAsync(CopilotSession session, string prompt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionEvent>> GetSessionMessagesAsync(CopilotSession session);
    Task<CopilotSession?> GetSessionAsync(string sessionId);
    /// <returns><c>true</c> if the session was found and deleted; <c>false</c> if the session did not exist.</returns>
    Task<bool> DeleteSessionAsync(string sessionId);
}

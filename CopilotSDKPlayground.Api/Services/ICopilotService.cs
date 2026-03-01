using GitHub.Copilot.SDK;

namespace CopilotSDKPlayground.Api.Services;

public interface ICopilotService
{
    Task InitializeAsync();
    Task<CopilotSession> CreateSessionAsync(string model, string? sessionId = null);
    Task<string> SendMessageAsync(CopilotSession session, string prompt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionEvent>> GetSessionMessagesAsync(CopilotSession session);
    Task<CopilotSession?> GetSessionAsync(string sessionId);
    /// <returns><c>true</c> if the session was found and deleted; <c>false</c> if the session did not exist.</returns>
    Task<bool> DeleteSessionAsync(string sessionId);
}

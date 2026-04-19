using StackExchange.Redis;

namespace CopilotSDKPlayground.Api.Services;

/// <summary>
/// Persists per-user conversation summaries in Redis.
/// When a session grows beyond <see cref="MaxTurnsBeforeCompaction"/> turns,
/// the service asks Copilot to summarise the conversation and stores the digest.
/// On the next session, the summary is injected as context so the model
/// "remembers" previous interactions.
/// </summary>
public class MemoryService
{
    private const int MaxTurnsBeforeCompaction = 10;
    private const string KeyPrefix = "copilot:memory:";

    private readonly IConnectionMultiplexer _redis;
    private readonly ICopilotService _copilotService;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        IConnectionMultiplexer redis,
        ICopilotService copilotService,
        ILogger<MemoryService> logger)
    {
        _redis = redis;
        _copilotService = copilotService;
        _logger = logger;
    }

    public async Task<string?> GetMemoryAsync(string userId)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync($"{KeyPrefix}{userId}");
        return value.HasValue ? value.ToString() : null;
    }

    public async Task SaveMemoryAsync(string userId, string summary)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"{KeyPrefix}{userId}", summary, TimeSpan.FromDays(30));
        _logger.LogInformation("Saved memory for user {UserId} ({Length} chars)", userId, summary.Length);
    }

    public async Task DeleteUserMemoryAsync(string userId)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"{KeyPrefix}{userId}");
        _logger.LogInformation("Deleted memory for user {UserId}", userId);
    }

    public async Task<DateTime?> GetLastUpdatedAsync(string userId)
    {
        var db = _redis.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync($"{KeyPrefix}{userId}");
        if (ttl is null) return null;
        return DateTime.UtcNow.Add(TimeSpan.FromDays(30) - ttl.Value);
    }

    /// <summary>
    /// Checks if the session has exceeded the turn limit and, if so,
    /// compacts the history into a summary stored in Redis.
    /// Call this after each exchange.
    /// </summary>
    public async Task MaybeCompactAsync(string userId, GitHub.Copilot.SDK.CopilotSession session)
    {
        var messages = await _copilotService.GetSessionMessagesAsync(session);
        // Count only user/assistant turns (2 events per turn)
        var turnCount = messages.Count(m =>
            m is GitHub.Copilot.SDK.UserMessageEvent or GitHub.Copilot.SDK.AssistantMessageEvent) / 2;

        if (turnCount < MaxTurnsBeforeCompaction) return;

        _logger.LogInformation("Compacting memory for user {UserId} after {Turns} turns", userId, turnCount);

        var history = string.Join("\n", messages.Select(m => m switch
        {
            GitHub.Copilot.SDK.UserMessageEvent u => $"User: {u.Data.Content}",
            GitHub.Copilot.SDK.AssistantMessageEvent a => $"Assistant: {a.Data.Content}",
            _ => null
        }).Where(l => l is not null));

        var compactionPrompt =
            $"Summarise this conversation in 3-5 bullet points. " +
            $"Capture key facts, decisions, and user preferences. " +
            $"Be concise.\n\n{history}";

        var summary = await _copilotService.SendMessageAsync(session, compactionPrompt);
        await SaveMemoryAsync(userId, summary);
    }
}

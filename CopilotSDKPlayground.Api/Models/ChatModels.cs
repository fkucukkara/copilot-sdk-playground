namespace CopilotSDKPlayground.Api.Models;

public class CreateSessionRequest
{
    public string? Model { get; set; }
    public string? SessionId { get; set; }
}

public class CreateSessionResponse
{
    public required string SessionId { get; set; }
    public required string Model { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatMessageRequest
{
    public required string Prompt { get; set; }
}

public class ChatMessageResponse
{
    public required string Content { get; set; }
    public string Role { get; set; } = "assistant";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SessionHistoryResponse
{
    public required string SessionId { get; set; }
    public required List<MessageEvent> Messages { get; set; }
}

public class MessageEvent
{
    public required string Type { get; set; }
    public required string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

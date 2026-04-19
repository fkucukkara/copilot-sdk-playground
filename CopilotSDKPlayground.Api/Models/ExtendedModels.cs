namespace CopilotSDKPlayground.Api.Models;

// ── Prompt Templates ────────────────────────────────────────────────────────

public class PromptTemplate
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string SystemPrompt { get; set; }
    public List<FewShotExample> FewShotExamples { get; set; } = [];
}

public class FewShotExample
{
    public required string User { get; set; }
    public required string Assistant { get; set; }
}

public class CreateTemplateSessionRequest
{
    public required string TemplateName { get; set; }
    public string? Model { get; set; }
    public string? SessionId { get; set; }
}

public class TemplateSessionResponse
{
    public required string SessionId { get; set; }
    public required string TemplateName { get; set; }
    public required string SystemPromptPreview { get; set; }
    public int FewShotExampleCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Tool Calling ─────────────────────────────────────────────────────────────

public class CreateToolSessionRequest
{
    public string? Model { get; set; }
    public string? SessionId { get; set; }
}

public class ToolSessionResponse
{
    public required string SessionId { get; set; }
    public required List<string> RegisteredTools { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AvailableToolsResponse
{
    public required List<ToolInfo> Tools { get; set; }
}

public class ToolInfo
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required List<ToolParameter> Parameters { get; set; }
}

public class ToolParameter
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Description { get; set; }
    public bool Required { get; set; }
}

// ── Redis Memory ─────────────────────────────────────────────────────────────

public class MemorySessionRequest
{
    public required string UserId { get; set; }
    public string? Model { get; set; }
}

public class MemorySessionResponse
{
    public required string SessionId { get; set; }
    public required string UserId { get; set; }
    public bool ContextLoaded { get; set; }
    public string? ContextSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class UserMemoryResponse
{
    public required string UserId { get; set; }
    public string? Summary { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public bool HasMemory { get; set; }
}

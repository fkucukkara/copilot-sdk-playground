namespace CopilotSDKPlayground.McpApi.Models;

/// <summary>Request to create a new MCP-backed Copilot chat session.</summary>
public record McpSessionRequest(string? Model = null, string? SessionId = null);

/// <summary>Response returned when a session is created.</summary>
public record McpSessionResponse(
    string SessionId,
    string Model,
    IReadOnlyList<string> AvailableTools);

/// <summary>Chat message request body.</summary>
public record McpMessageRequest(string Message);

/// <summary>Chat message response body.</summary>
public record McpMessageResponse(string Response, string SessionId);

/// <summary>Summary of a single MCP tool exposed by the McpServer.</summary>
public record McpToolInfo(string Name, string? Description);

/// <summary>List of all tools discovered from the McpServer.</summary>
public record McpToolsListResponse(IReadOnlyList<McpToolInfo> Tools, string McpServerEndpoint);

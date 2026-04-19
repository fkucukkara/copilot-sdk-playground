using CopilotSDKPlayground.McpApi.Models;
using CopilotSDKPlayground.McpApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CopilotSDKPlayground.McpApi.Endpoints;

/// <summary>
/// MCP + Copilot SDK integration endpoints.
///
/// Demonstration flow:
///   1. GET /api/mcp/tools   — inspect what tools the MCP server exposes
///   2. POST /api/mcp/sessions — create a Copilot session backed by MCP tools
///   3. POST /api/mcp/{sessionId}/messages — chat; Copilot will call MCP tools as needed
///   4. DELETE /api/mcp/{sessionId} — clean up
/// </summary>
public static class McpEndpoints
{
    public static IEndpointRouteBuilder MapMcpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mcp").WithTags("MCP + Copilot");

        group.MapGet("/tools", ListTools)
             .WithSummary("List all tools discovered from the MCP server");

        group.MapPost("/sessions", CreateSession)
             .WithSummary("Create a Copilot session with MCP tools pre-loaded");

        group.MapPost("/{sessionId}/messages", SendMessage)
             .WithSummary("Send a message — Copilot calls MCP tools automatically when needed");

        group.MapDelete("/{sessionId}", DeleteSession)
             .WithSummary("Delete a session");

        return app;
    }

    private static Ok<McpToolsListResponse> ListTools(McpCopilotService service) =>
        TypedResults.Ok(new McpToolsListResponse(
            Tools: service.GetToolDescriptions()
                .Select(t => new McpToolInfo(t.Name, t.Description))
                .ToList(),
            McpServerEndpoint: service.McpEndpoint));

    private static async Task<Created<McpSessionResponse>> CreateSession(
        McpSessionRequest request,
        McpCopilotService service,
        IConfiguration configuration)
    {
        var model = request.Model ?? configuration.GetValue<string>("CopilotSDK:DefaultModel", "gpt-4o")!;
        var (sessionId, toolNames) = await service.CreateSessionAsync(model, request.SessionId);

        return TypedResults.Created($"/api/mcp/{sessionId}", new McpSessionResponse(
            SessionId: sessionId,
            Model: model,
            AvailableTools: toolNames));
    }

    private static async Task<Results<Ok<McpMessageResponse>, NotFound>> SendMessage(
        string sessionId,
        McpMessageRequest request,
        McpCopilotService service,
        HttpContext context)
    {
        if (!service.SessionExists(sessionId))
            return TypedResults.NotFound();

        var response = await service.SendMessageAsync(sessionId, request.Message, context.RequestAborted);
        return TypedResults.Ok(new McpMessageResponse(Response: response, SessionId: sessionId));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteSession(
        string sessionId,
        McpCopilotService service)
    {
        return await service.DeleteSessionAsync(sessionId)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }
}

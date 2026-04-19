using CopilotSDKPlayground.Api.Models;
using CopilotSDKPlayground.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CopilotSDKPlayground.Api.Endpoints;

/// <summary>
/// Demonstrates the Copilot SDK tool/function calling feature.
/// Sessions are created with custom tools registered via <c>AIFunctionFactory.Create</c>.
/// When the model decides to call a tool, the SDK invokes the handler automatically
/// and feeds the result back — transparent to the caller.
/// </summary>
public static class ToolsEndpoints
{
    public static IEndpointRouteBuilder MapToolsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tools").WithTags("Tool Calling");

        group.MapGet("/available", GetAvailableTools)
             .WithSummary("List tools registered in demo tool-calling sessions");

        group.MapPost("/sessions", CreateToolSession)
             .WithSummary("Create a session with get_weather, calculate, and get_current_time tools");

        group.MapPost("/{sessionId}/messages", SendToolMessage)
             .WithSummary("Send a prompt — Copilot will call tools automatically as needed");

        group.MapDelete("/{sessionId}", DeleteSession)
             .WithSummary("Delete a tool-enabled session");

        return app;
    }

    private static Ok<AvailableToolsResponse> GetAvailableTools() =>
        TypedResults.Ok(new AvailableToolsResponse
        {
            Tools =
            [
                new ToolInfo
                {
                    Name = "get_weather",
                    Description = "Returns the current weather for a city.",
                    Parameters =
                    [
                        new ToolParameter { Name = "city", Type = "string", Description = "City name, e.g. 'London'", Required = true },
                        new ToolParameter { Name = "unit", Type = "string", Description = "celsius or fahrenheit", Required = false }
                    ]
                },
                new ToolInfo
                {
                    Name = "calculate",
                    Description = "Evaluates a mathematical expression.",
                    Parameters =
                    [
                        new ToolParameter { Name = "expression", Type = "string", Description = "Math expression, e.g. '(12 + 4) * 3'", Required = true }
                    ]
                },
                new ToolInfo
                {
                    Name = "get_current_time",
                    Description = "Returns current date and time for a timezone.",
                    Parameters =
                    [
                        new ToolParameter { Name = "timezone", Type = "string", Description = "IANA timezone, e.g. 'Europe/London'", Required = false }
                    ]
                }
            ]
        });

    private static async Task<Created<ToolSessionResponse>> CreateToolSession(
        CreateToolSessionRequest request,
        ToolCopilotService toolService,
        IConfiguration configuration)
    {
        var model = request.Model ?? configuration.GetValue<string>("CopilotSDK:DefaultModel", "gpt-4o")!;
        var session = await toolService.CreateToolSessionAsync(model, request.SessionId);

        return TypedResults.Created($"/api/tools/{session.SessionId}", new ToolSessionResponse
        {
            SessionId = session.SessionId,
            RegisteredTools = [.. ToolCopilotService.AvailableToolNames]
        });
    }

    private static async Task<Results<Ok<ChatMessageResponse>, NotFound>> SendToolMessage(
        string sessionId,
        ChatMessageRequest request,
        ICopilotService copilotService,
        HttpContext context)
    {
        var session = await copilotService.GetSessionAsync(sessionId);
        if (session is null)
            return TypedResults.NotFound();

        var content = await copilotService.SendMessageAsync(session, request.Prompt, context.RequestAborted);
        return TypedResults.Ok(new ChatMessageResponse { Content = content });
    }

    private static async Task<Results<NoContent, NotFound>> DeleteSession(
        string sessionId,
        ICopilotService copilotService)
    {
        var deleted = await copilotService.DeleteSessionAsync(sessionId);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}

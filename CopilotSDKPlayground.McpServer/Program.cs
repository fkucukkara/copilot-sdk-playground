using CopilotSDKPlayground.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── MCP Server ────────────────────────────────────────────────────────────────
// Stateless mode is recommended for servers that don't need server-to-client
// requests (sampling, elicitation). It simplifies deployment and scaling.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<WeatherTool>()
    .WithTools<CalculatorTool>()
    .WithTools<TimeTool>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Maps the MCP endpoint at /mcp (Streamable HTTP transport)
app.MapMcp("/mcp");

app.Run();

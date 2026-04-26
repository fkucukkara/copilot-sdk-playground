using CopilotSDKPlayground.McpApi.Endpoints;
using CopilotSDKPlayground.McpApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── MCP + Copilot services ────────────────────────────────────────────────────
builder.Services.AddSingleton<McpCopilotService>();

// Named HttpClient for the MCP server.
// Aspire service discovery (enabled by AddServiceDefaults) resolves
// "mcp-server" to the actual endpoint injected by AppHost.WithReference.
builder.Services.AddHttpClient("mcp-server", client =>
{
    client.BaseAddress = new Uri("http://mcp-server/");
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Initialize Copilot client and connect to the MCP server.
// Wrapped in try/catch so the app can still start when running standalone
// (without Aspire) and the MCP server is not available.
try
{
    await app.Services.GetRequiredService<McpCopilotService>().InitializeAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "MCP server is not reachable at startup. Tool features will be unavailable until the server comes online.");
}

app.MapMcpEndpoints();

app.Run();

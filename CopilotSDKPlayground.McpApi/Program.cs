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

// Initialize Copilot client and connect to the MCP server
await app.Services.GetRequiredService<McpCopilotService>().InitializeAsync();

app.MapMcpEndpoints();

app.Run();

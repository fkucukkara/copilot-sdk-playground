using CopilotSDKPlayground.AgentsApi.Endpoints;
using CopilotSDKPlayground.AgentsApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── Agent services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<CoordinatorAgentService>();
builder.Services.AddSingleton<OrchestratorAgentService>();

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Initialize both Copilot clients
await Task.WhenAll(
    app.Services.GetRequiredService<CoordinatorAgentService>().InitializeAsync(),
    app.Services.GetRequiredService<OrchestratorAgentService>().InitializeAsync());

app.MapAgentEndpoints();

app.Run();

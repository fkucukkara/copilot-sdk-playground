using CopilotSDKPlayground.Api.Endpoints;
using CopilotSDKPlayground.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults from Aspire (OpenTelemetry, resilience, service discovery, health checks)
builder.AddServiceDefaults();

// ── Core Copilot service ─────────────────────────────────────────────────────
builder.Services.AddSingleton<ICopilotService, CopilotService>();

// ── Extended features ────────────────────────────────────────────────────────
builder.Services.AddSingleton<IStreamingCopilotService, StreamingCopilotService>();
builder.Services.AddSingleton<ToolCopilotService>();
builder.Services.AddSingleton<PromptTemplateService>();

// ── Redis-backed memory (Aspire-provisioned Redis) ───────────────────────────
builder.AddRedisClient("cache");
builder.Services.AddSingleton<MemoryService>();

// ── OpenAPI ──────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

var app = builder.Build();

// Map service defaults (health checks, etc.)
app.MapDefaultEndpoints();

// Configure OpenAPI in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Initialize Copilot SDK client
await app.Services.GetRequiredService<ICopilotService>().InitializeAsync();

// ── Endpoint registration ────────────────────────────────────────────────────
app.MapChatEndpoints();
app.MapStreamingEndpoints();
app.MapToolsEndpoints();
app.MapPromptTemplateEndpoints();
app.MapMemoryEndpoints();

app.Run();

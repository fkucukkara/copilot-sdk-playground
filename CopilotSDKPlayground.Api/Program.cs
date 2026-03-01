using CopilotSDKPlayground.Api.Endpoints;
using CopilotSDKPlayground.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults from Aspire (OpenTelemetry, resilience, service discovery, health checks)
builder.AddServiceDefaults();

// Add Copilot SDK service
builder.Services.AddSingleton<ICopilotService, CopilotService>();

// Add OpenAPI/Swagger
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

app.MapChatEndpoints();

app.Run();

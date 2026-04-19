using CopilotSDKPlayground.RagApi.Endpoints;
using CopilotSDKPlayground.RagApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── RAG services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<InMemoryVectorStore>();
builder.Services.AddSingleton<RagCopilotService>();

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Initialize Copilot client
await app.Services.GetRequiredService<RagCopilotService>().InitializeAsync();

// Seed the knowledge base with sample documents for instant demo
SeedKnowledgeBase(app.Services.GetRequiredService<InMemoryVectorStore>());

app.MapDocumentEndpoints();
app.MapRagChatEndpoints();

app.Run();

// ── Seed data ─────────────────────────────────────────────────────────────────
static void SeedKnowledgeBase(InMemoryVectorStore store)
{
    store.Ingest(
        ".NET 10 Key Features",
        """
        .NET 10 is the latest Long-Term Support (LTS) release of the .NET platform,
        scheduled for November 2025. It ships with C# 14, which introduces primary
        constructor improvements, field keyword support for auto-properties, and
        enhanced pattern matching with list patterns. The runtime gains significant
        performance improvements in the JIT compiler, including improved loop
        optimisations and better SIMD vectorisation. ASP.NET Core 10 introduces
        built-in support for OpenAPI 3.1, a new minimal API group-level filters API,
        and improved SignalR reliability. The BCL adds new LINQ methods including
        Index(), CountBy(), and AggregateBy(). Blazor receives enhanced JavaScript
        interop performance and a new static SSR rendering mode for improved SEO.
        Entity Framework Core 10 brings breaking query translation improvements and
        better support for complex owned types. The SDK introduces a new dotnet publish
        workflow with native AOT improvements and smaller published sizes.
        """,
        source: "Microsoft .NET Blog");

    store.Ingest(
        ".NET Aspire Overview",
        """
        .NET Aspire is an opinionated stack for building observable, production-ready
        distributed applications. It is delivered as a set of NuGet packages and
        project templates that simplify wiring up common cloud-native patterns.
        The AppHost project acts as the orchestration layer: it declares the application's
        topology using a resource-based model where each service, container, or cloud
        resource is a node in the application graph. Aspire automatically injects
        connection strings and service URLs into dependent projects via environment
        variables, eliminating manual configuration. The built-in dashboard provides
        real-time traces, metrics, and structured logs powered by OpenTelemetry.
        ServiceDefaults is a shared project that adds OpenTelemetry exporters, health
        checks, HTTP resilience pipelines, and service discovery with a single
        AddServiceDefaults() call. Aspire supports integrations for Redis, PostgreSQL,
        RabbitMQ, Azure Service Bus, Azure Blob Storage, and many others through
        Aspire.Hosting.* packages. Component packages like Aspire.StackExchange.Redis
        add production-grade configuration, health checks, and telemetry automatically.
        """,
        source: "Microsoft Aspire Documentation");

    store.Ingest(
        "GitHub Copilot SDK for .NET",
        """
        The GitHub Copilot SDK for .NET (NuGet: GitHub.Copilot.SDK) provides
        programmatic access to GitHub Copilot's AI capabilities from .NET applications.
        The SDK manages the lifecycle of the Copilot CLI process and exposes an
        async API for creating sessions and sending messages. A CopilotClient instance
        should be treated as a singleton: create it once, call StartAsync(), and reuse
        it across the application lifetime. Sessions are created via CreateSessionAsync
        with a SessionConfig that specifies the model, system message, tools, and
        streaming preferences. The SDK supports tool/function calling through
        AIFunctionFactory.Create from Microsoft.Extensions.AI — register tools in
        SessionConfig.Tools and the SDK will invoke them automatically when the model
        calls them. Streaming is enabled by setting Streaming = true in SessionConfig,
        which causes AssistantMessageDeltaEvent to fire per token chunk. The
        PermissionHandler controls which built-in Copilot tools are allowed;
        PermissionHandler.ApproveAll is used in demos but production code should
        implement selective approval logic. Sessions are disposed asynchronously via
        DisposeAsync() and the client is stopped with StopAsync().
        """,
        source: "GitHub Copilot SDK README");
}

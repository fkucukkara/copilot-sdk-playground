var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ─────────────────────────────────────────────────────────────
// Redis is used by the Api project for conversation memory persistence.
var redis = builder.AddRedis("cache");

// ── Projects ───────────────────────────────────────────────────────────────────

// Original API — chat, streaming, tool calling, prompt templates, Redis memory
builder.AddProject<Projects.CopilotSDKPlayground_Api>("copilot-api")
    .WithReference(redis)
    .WithExternalHttpEndpoints();

// RAG — in-memory TF-IDF vector store + GitHub Copilot SDK
builder.AddProject<Projects.CopilotSDKPlayground_RagApi>("rag-api")
    .WithExternalHttpEndpoints();

// MCP Server — exposes WeatherTool, CalculatorTool, TimeTool via Streamable HTTP
var mcpServer = builder.AddProject<Projects.CopilotSDKPlayground_McpServer>("mcp-server")
    .WithExternalHttpEndpoints();

// MCP API — Copilot SDK sessions backed by MCP server tools
// WithReference injects Aspire service discovery so "mcp-server" resolves automatically
// WaitFor ensures mcp-server is healthy before mcp-api starts connecting
builder.AddProject<Projects.CopilotSDKPlayground_McpApi>("mcp-api")
    .WithReference(mcpServer)
    .WaitFor(mcpServer)
    .WithExternalHttpEndpoints();

// Agents API — Coordinator pattern + Orchestrator/sub-agent parallel pattern
builder.AddProject<Projects.CopilotSDKPlayground_AgentsApi>("agents-api")
    .WithExternalHttpEndpoints();

builder.Build().Run();

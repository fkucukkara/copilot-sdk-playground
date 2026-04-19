# CopilotSDK Playground

A comprehensive demonstration of [GitHub Copilot SDK for .NET](https://github.com/github/copilot-sdk) capabilities, spanning five API projects that cover core chat, **RAG (Retrieval-Augmented Generation)**, **MCP tool servers**, and **multi-agent orchestration** — all wired together with **.NET 10** and **.NET Aspire**.

## Solution Overview

| Project | Port | Purpose |
|---------|------|---------|
| `CopilotSDKPlayground.Api` | 5100 | Core chat API with streaming SSE, tool calling, prompt templates, and Redis memory |
| `CopilotSDKPlayground.RagApi` | 5200 | RAG pipeline: ingest documents → TF-IDF retrieval → grounded Copilot responses |
| `CopilotSDKPlayground.McpServer` | 5300 | Standalone MCP tool server (Weather, Calculator, Time) |
| `CopilotSDKPlayground.McpApi` | 5400 | Copilot sessions powered by MCP tools via ModelContextProtocol SDK |
| `CopilotSDKPlayground.AgentsApi` | 5500 | Multi-agent patterns: Coordinator/Specialist + Orchestrator/SubAgent |

```
+------------------------------------------------------------------+
|                     .NET Aspire AppHost                          |
|                   (Aspire Dashboard + Redis)                     |
|                                                                  |
|  +----------+  +----------+  +-----------+  +------------+      |
|  |    Api   |  |  RagApi  |  | McpServer |  | AgentsApi  |      |
|  |streaming |  | TF-IDF   |  |  Weather  |  |Coordinator |      |
|  |  tools   |  | retrieval|  |  Calc     |  |Orchestrator|      |
|  |prompts   |  | grounded |  |  Time     |  |            |      |
|  |  Redis   |  | answers  |  +-----+-----+  +------------+      |
|  +----------+  +----------+        |                            |
|                             +------v------+                     |
|                             |   McpApi    |                     |
|                             |Copilot+tools|                     |
|                             +-------------+                     |
+------------------------------------------------------------------+
```

## Prerequisites

| Requirement | Notes |
|-------------|-------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Version pinned in `global.json` |
| GitHub CLI | `winget install GitHub.cli` |
| Copilot CLI extension | `gh extension install github/gh-copilot` |
| GitHub authentication | `gh auth login` |
| GitHub Copilot subscription | Required for API access |
| Docker Desktop | Required by Aspire for the Redis container |
| Visual Studio 2022 17.13+ or VS Code + C# Dev Kit | |

## Quick Start

```bash
# 1. Clone
git clone https://github.com/yourusername/CopilotSDKPlayground.git
cd CopilotSDKPlayground

# 2. Restore
dotnet restore

# 3. Run all projects via Aspire (recommended)
dotnet run --project CopilotSDKPlayground.AppHost
```

Aspire starts all five projects plus a Redis container and opens the **dashboard** at `https://localhost:17146`.

Each project also runs standalone for focused development:

```bash
dotnet run --project CopilotSDKPlayground.Api
dotnet run --project CopilotSDKPlayground.RagApi
dotnet run --project CopilotSDKPlayground.McpServer  # start first when testing McpApi
dotnet run --project CopilotSDKPlayground.McpApi
dotnet run --project CopilotSDKPlayground.AgentsApi
```

---

## Project Details

### 1. Api — Core Chat + Extended Features

**Path:** `CopilotSDKPlayground.Api/`
**HTTP examples:** `CopilotSDKPlayground.Api/chat-examples.http`

Demonstrates the full breadth of the Copilot SDK session capabilities.

| Feature | Endpoint group | Description |
|---------|---------------|-------------|
| **Basic chat** | `/api/chat` | Create / query / history / delete sessions |
| **Streaming SSE** | `/api/streaming` | Server-Sent Events responses via `Channel<string>` |
| **Tool calling** | `/api/tools` | AI-invoked functions (weather, calculator, clock) using `AIFunctionFactory.Create` |
| **Prompt templates** | `/api/prompts` | Parameterised system prompts with `SystemMessageMode.Replace` |
| **Redis memory** | `/api/memory` | Persistent conversation summaries stored in Redis |

**Key SDK patterns:**
- `SessionConfig { Streaming = true }` → `AssistantMessageDeltaEvent.Data.DeltaContent`
- `AIFunctionFactory.Create` with local methods + `[Description]` attributes on parameters
- `SessionConfig.Tools = [ tool1, tool2, tool3 ]`
- `SessionConfig.SystemMessage = new SystemMessageConfig { Mode = SystemMessageMode.Replace, Content = "..." }`

---

### 2. RagApi — Retrieval-Augmented Generation

**Path:** `CopilotSDKPlayground.RagApi/`
**HTTP examples:** `CopilotSDKPlayground.RagApi/rag-examples.http`

A complete RAG pipeline built in pure C# with no vector database dependencies.

#### Architecture

```
User query
    |
    v
InMemoryVectorStore.Search(query, topK=3)   <- TF-IDF cosine similarity
    |                                          Sentence-aware sliding window chunking
    v                                          Sparse Dictionary<string, double> vectors
RagCopilotService.BuildAugmentedPrompt()    <- Injects retrieved chunks + similarity scores
    |
    v
Copilot SDK session                         <- Grounded response with inline citations
```

**Documents are pre-seeded on startup:**
- `.NET 10 Key Features`
- `.NET Aspire Overview`
- `GitHub Copilot SDK for .NET`

**Key implementation details:**
- Chunking: sentence-aware sliding window (`TargetChunkWords=512`, `OverlapWords=52`)
- Vectorisation: `log(1 + tf)` weighting; stop-word filtered
- Similarity: cosine on sparse dictionaries (no ML dependency)
- System prompt: `SystemMessageMode.Replace` with explicit "only answer from context" instruction

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/rag/documents` | GET | List ingested documents |
| `/api/rag/documents` | POST | Ingest a new document (title + content + optional source) |
| `/api/rag/documents/{id}` | DELETE | Remove document and its chunks |
| `/api/rag/sessions` | POST | Create a RAG chat session |
| `/api/rag/{sessionId}/query` | POST | Query with retrieval — response includes `retrievedChunks` |
| `/api/rag/{sessionId}` | DELETE | Delete session |

---

### 3. McpServer — Model Context Protocol Tool Server

**Path:** `CopilotSDKPlayground.McpServer/`
**Package:** `ModelContextProtocol.AspNetCore 1.2.0`

A stateless HTTP MCP server exposing three tools.

```csharp
// The complete MCP server registration in Program.cs
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<WeatherTool>()
    .WithTools<CalculatorTool>()
    .WithTools<TimeTool>();

app.MapMcp("/mcp");
```

| Tool | Name | Description |
|------|------|-------------|
| `WeatherTool` | `get_weather` | Mock weather for 10 cities (Celsius/Fahrenheit) |
| `CalculatorTool` | `calculate` | Safe arithmetic via `DataTable.Compute` |
| `TimeTool` | `get_current_time` | Current time in any IANA timezone |

**Key pattern:** `[McpServerToolType]` on the class + `[McpServerTool(Name="...")]` on each method. Stateless transport is recommended for HTTP-hosted servers that don't need server-initiated requests.

---

### 4. McpApi — Copilot + MCP Bridge

**Path:** `CopilotSDKPlayground.McpApi/`
**HTTP examples:** `CopilotSDKPlayground.McpApi/mcp-examples.http`
**Packages:** `GitHub.Copilot.SDK 0.1.29`, `ModelContextProtocol 1.2.0`

Bridges the MCP server's tools into a Copilot SDK session. Copilot automatically calls tools during conversation.

#### Initialization flow

```
McpCopilotService.InitializeAsync()
    |
    +- HttpClientTransport({ Endpoint = "http://mcp-server/mcp" }, httpClient)
    |   +- Aspire service discovery resolves "mcp-server" hostname automatically
    |
    +- McpClient.CreateAsync(transport)
    |
    +- McpClient.ListToolsAsync()  ->  IList<McpClientTool>
                                        McpClientTool : AIFunction
                                                          |
                                         SessionConfig.Tools = [.. _mcpTools]
                                                          |
                                         Copilot SDK invokes tools transparently
```

**Key insight:** `McpClientTool` inherits `AIFunction` (from `Microsoft.Extensions.AI`), so MCP tools plug directly into `SessionConfig.Tools` without any adapter code.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/mcp/tools` | GET | List discovered MCP tools |
| `/api/mcp/sessions` | POST | Create Copilot session with MCP tools loaded |
| `/api/mcp/{sessionId}/messages` | POST | Chat — Copilot calls MCP tools as needed |
| `/api/mcp/{sessionId}` | DELETE | Delete session |

---

### 5. AgentsApi — Multi-Agent Orchestration

**Path:** `CopilotSDKPlayground.AgentsApi/`
**HTTP examples:** `CopilotSDKPlayground.AgentsApi/agents-examples.http`

Demonstrates two multi-agent patterns using only the Copilot SDK and ephemeral sessions.

#### Pattern A: Coordinator / Specialist

```
User task
    |
    v
Coordinator agent (ephemeral session)
    Returns JSON: { "agent": "Code|Research|Writing|Data", "reasoning": "..." }
    |
    v
Specialist agent (ephemeral session, dedicated system prompt)
    Returns: final answer
```

Specialists: `Research`, `Code`, `Writing`, `Data` — each with a focused system prompt via `SystemMessageMode.Replace`.

**Endpoint:** `POST /api/agents/coordinator`

#### Pattern B: Orchestrator / SubAgents (Parallel)

```
User goal
    |
    v
Orchestrator agent (ephemeral session)
    Returns JSON array: [{ "id", "description", "assignedAgent" }, ...]
    |
    v  Task.WhenAll  (true parallelism)
    +- SubAgent: Researcher  (ephemeral)
    +- SubAgent: Developer   (ephemeral)
    +- SubAgent: Analyst     (ephemeral)
    +- SubAgent: Writer      (ephemeral)
    |
    v
Synthesis agent (ephemeral session)
    Combines all sub-task results into a final response
```

**Endpoint:** `POST /api/agents/orchestrator`

**Key pattern:** All agent invocations use `await using var session = await _client.CreateSessionAsync(...)` — ephemeral sessions auto-disposed after each call, preventing accumulation.

---

## Configuration

All projects share these configuration keys:

```json
{
  "CopilotSDK": {
    "DefaultModel": "gpt-4o",
    "LogLevel": "info"
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `DefaultModel` | `gpt-4o` | Fallback model when none is specified in the request |
| `LogLevel` | `info` | SDK log verbosity: `debug` \| `info` \| `warn` \| `error` |

McpApi also reads `McpServer:BaseUrl` (default: `http://localhost:5300`) when running without Aspire.

### Available models

- `gpt-4o` (default)
- `gpt-4`
- `claude-sonnet-4.5`
- `o1`

---

## Repository Structure

```
CopilotSDKPlayground/
+-- CopilotSDKPlayground.Api/           # Core chat + streaming/tools/prompts/memory
|   +-- Endpoints/
|   |   +-- ChatEndpoints.cs
|   |   +-- StreamingEndpoints.cs
|   |   +-- ToolsEndpoints.cs
|   |   +-- PromptTemplateEndpoints.cs
|   |   +-- MemoryEndpoints.cs
|   +-- Models/ChatModels.cs, ExtendedModels.cs
|   +-- Services/CopilotService.cs, StreamingCopilotService.cs, ToolCopilotService.cs, ...
|   +-- chat-examples.http
+-- CopilotSDKPlayground.RagApi/        # RAG pipeline
|   +-- Endpoints/DocumentEndpoints.cs, RagChatEndpoints.cs
|   +-- Models/RagModels.cs
|   +-- Services/DocumentChunker.cs, InMemoryVectorStore.cs, RagCopilotService.cs
|   +-- rag-examples.http
+-- CopilotSDKPlayground.McpServer/     # MCP tool server
|   +-- Tools/WeatherTool.cs, CalculatorTool.cs, TimeTool.cs
|   +-- Program.cs
+-- CopilotSDKPlayground.McpApi/        # Copilot + MCP bridge
|   +-- Endpoints/McpEndpoints.cs
|   +-- Models/McpModels.cs
|   +-- Services/McpCopilotService.cs
|   +-- mcp-examples.http
+-- CopilotSDKPlayground.AgentsApi/     # Multi-agent patterns
|   +-- Endpoints/AgentEndpoints.cs
|   +-- Models/AgentModels.cs
|   +-- Services/CoordinatorAgentService.cs, OrchestratorAgentService.cs
|   +-- agents-examples.http
+-- CopilotSDKPlayground.AppHost/       # Aspire orchestration (Redis + all 5 projects)
+-- CopilotSDKPlayground.ServiceDefaults/ # Shared OTel, health checks, resilience
+-- Directory.Build.props               # Shared build settings
+-- global.json                         # .NET 10 SDK pin
```

---

## Observability

Run via Aspire for the full observability stack at `https://localhost:17146`:

| Signal | What you get |
|--------|-------------|
| **Traces** | Distributed tracing across all HTTP requests |
| **Metrics** | ASP.NET Core, HTTP client, and runtime counters |
| **Logs** | Structured logs correlated with trace and span IDs |
| **Resources** | Live view of all 5 APIs + Redis container |

---

## Building

```bash
dotnet build
dotnet build -p:TreatWarningsAsErrors=true
```

---

## Troubleshooting

### `Copilot CLI executable not found`

```bash
winget install GitHub.cli
gh extension install github/gh-copilot
gh auth login
gh copilot --version  # verify
```

### McpApi cannot connect to McpServer

When running standalone (without Aspire), start McpServer first, then McpApi.
Override the URL in `CopilotSDKPlayground.McpApi/appsettings.Development.json`:
```json
{ "McpServer": { "BaseUrl": "http://localhost:5300" } }
```

### Redis connection failure

Redis is provided as a container by Aspire. For standalone development, start Redis locally:
```bash
docker run -p 6379:6379 redis:alpine
```

### Port conflicts

Adjust ports in each project's `Properties/launchSettings.json`.

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit your changes: `git commit -m 'Add my feature'`
4. Push: `git push origin feature/my-feature`
5. Open a Pull Request

## License

MIT — see [LICENSE](LICENSE).

## Resources

- [GitHub Copilot SDK for .NET](https://github.com/github/copilot-sdk)
- [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/)
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)

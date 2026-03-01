# CopilotSDK Playground

A demonstration of [GitHub Copilot SDK](https://github.com/github/copilot-sdk) capabilities using **.NET 10** and **.NET Aspire**. The project provides a production-style ASP.NET Core minimal API for creating and managing Copilot chat sessions, with enterprise-grade observability, health checks, and resilience patterns built in.

## Features

- **Chat Sessions** — Create, query, and delete AI-powered chat sessions with configurable models
- **Observability** — Full OpenTelemetry integration (traces, metrics, structured logs) via .NET Aspire
- **Health Checks** — Built-in liveness and readiness probes via Aspire service defaults
- **Resilience** — Standard HTTP resilience pipeline (retries, circuit breakers) from Aspire
- **OpenAPI** — Auto-generated API documentation at `/openapi/v1.json`

## Prerequisites

| Requirement | Notes |
|-------------|-------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Version pinned in `global.json` |
| GitHub CLI | `winget install GitHub.cli` |
| Copilot CLI extension | `gh extension install github/gh-copilot` |
| GitHub authentication | `gh auth login` |
| GitHub Copilot subscription | Required for API access |
| Visual Studio 2022 17.13+ or VS Code + C# Dev Kit | |

## Quick Start

### 1. Clone

```bash
git clone https://github.com/yourusername/CopilotSDKPlayground.git
cd CopilotSDKPlayground
```

### 2. Restore

```bash
dotnet restore
```

### 3. Run with Aspire (recommended)

```bash
dotnet run --project CopilotSDKPlayground.AppHost
```

This starts the API and opens the **Aspire Dashboard** at `https://localhost:17146`.

### 4. Run the API standalone (optional)

```bash
dotnet run --project CopilotSDKPlayground.Api
```

| Protocol | URL |
|----------|-----|
| HTTPS | `https://localhost:7123` |
| HTTP  | `http://localhost:5123`  |

## Architecture

```
CopilotSDKPlayground/
├── CopilotSDKPlayground.Api/              # Main API project
│   ├── Endpoints/
│   │   └── ChatEndpoints.cs              # Chat session endpoints
│   ├── Models/
│   │   └── ChatModels.cs                 # Request / response DTOs
│   ├── Services/
│   │   ├── ICopilotService.cs            # Service abstraction
│   │   └── CopilotService.cs             # Copilot SDK implementation
│   ├── chat-examples.http                # HTTP request samples
│   └── Program.cs                        # Application entry point
├── CopilotSDKPlayground.AppHost/         # Aspire orchestration
└── CopilotSDKPlayground.ServiceDefaults/ # Shared Aspire defaults (OTel, health, resilience)
```

### Design Principles

- **Interface-Driven** — `ICopilotService` abstracts Copilot SDK details from the endpoint layer
- **Minimal API** — Modern endpoint routing with `MapGroup` and `TypedResults`
- **Singleton + `IAsyncDisposable`** — One `CopilotClient` for the process lifetime; sessions are disposed on shutdown
- **DTO Pattern** — Request/response models are decoupled from SDK types
- **Aspire Integration** — Service defaults provide OpenTelemetry, health checks, and resilience with zero boilerplate

## API Reference

Base path: `/api/chat`

### Create a session

```http
POST /api/chat/sessions
Content-Type: application/json

{
  "model": "gpt-4o",
  "sessionId": "optional-custom-id"
}
```

Both fields are optional. Omitting `model` falls back to `CopilotSDK:DefaultModel` from configuration.

**Response `201 Created`:**

```json
{
  "sessionId": "abc123",
  "model": "gpt-4o",
  "createdAt": "2026-03-01T10:30:00Z"
}
```

---

### Send a message

```http
POST /api/chat/{sessionId}/messages
Content-Type: application/json

{
  "prompt": "What is 2+2?"
}
```

**Response `200 OK`:**

```json
{
  "content": "2+2 equals 4.",
  "role": "assistant",
  "timestamp": "2026-03-01T10:31:00Z"
}
```

Returns `404 Not Found` when the session does not exist.  
Supports cooperative cancellation via the request's `CancellationToken`.

---

### Get session history

```http
GET /api/chat/{sessionId}/history
```

**Response `200 OK`:**

```json
{
  "sessionId": "abc123",
  "messages": [
    { "type": "UserMessageEvent",      "content": "What is 2+2?", "timestamp": "..." },
    { "type": "AssistantMessageEvent", "content": "2+2 equals 4.", "timestamp": "..." }
  ]
}
```

Returns `404 Not Found` when the session does not exist.

---

### Delete a session

```http
DELETE /api/chat/{sessionId}
```

**Response:** `204 No Content` on success, `404 Not Found` when the session does not exist.

---

### Health endpoints (development only)

```http
GET /health    # All health checks must pass (readiness)
GET /alive     # Only "live"-tagged checks must pass (liveness)
```

## Configuration

`appsettings.json`:

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
| `DefaultModel` | `gpt-4o` | Model used when none is specified in the request |
| `LogLevel` | `info` | SDK internal log level: `debug` \| `info` \| `warn` \| `error` |

Override any value in `appsettings.Development.json` or via environment variables (standard ASP.NET Core configuration).

### Available models

- `gpt-4o` (default)
- `gpt-4`
- `claude-sonnet-4.5`
- `o1`

See the [Copilot CLI documentation](https://github.com/github/gh-copilot) for the full list.

## Testing

`CopilotSDKPlayground.Api/chat-examples.http` can be run directly in **Visual Studio** or **VS Code** (with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension).

### curl

```bash
# Create a session
curl -X POST https://localhost:7123/api/chat/sessions \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-4o"}'

# Send a message (replace SESSION_ID)
curl -X POST https://localhost:7123/api/chat/SESSION_ID/messages \
  -H "Content-Type: application/json" \
  -d '{"prompt":"What is 2+2?"}'

# Get history
curl https://localhost:7123/api/chat/SESSION_ID/history

# Delete the session
curl -X DELETE https://localhost:7123/api/chat/SESSION_ID
```

## Observability

Run via Aspire to get the full stack at `https://localhost:17146`:

| Signal | What you get |
|--------|-------------|
| **Traces** | Distributed tracing across all HTTP requests (health check requests are filtered out) |
| **Metrics** | ASP.NET Core, HTTP client, and runtime counters |
| **Logs** | Structured logs correlated with trace and span IDs |

Telemetry is exported via OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

## Building

```bash
# Build all projects
dotnet build

# Nullable warnings are already treated as errors via Directory.Build.props;
# promote all other warnings too:
dotnet build -p:TreatWarningsAsErrors=true
```

## Troubleshooting

### `Copilot CLI executable not found`

1. `winget install GitHub.cli`
2. `gh extension install github/gh-copilot`
3. `gh auth login`

### Authentication errors

```bash
gh auth login
gh copilot --version   # verify the extension is installed
```

### Port already in use

Change the ports in `CopilotSDKPlayground.Api/Properties/launchSettings.json`.

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit your changes: `git commit -m 'Add my feature'`
4. Push: `git push origin feature/my-feature`
5. Open a Pull Request

## License

MIT — see [LICENSE](LICENSE).

## Resources

- [GitHub Copilot SDK](https://github.com/github/copilot-sdk)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)

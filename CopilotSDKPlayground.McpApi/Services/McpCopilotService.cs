using System.Collections.Concurrent;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace CopilotSDKPlayground.McpApi.Services;

/// <summary>
/// Bridges the GitHub Copilot SDK with an MCP server.
///
/// Initialization flow:
///   1. <see cref="InitializeAsync"/> starts the Copilot client and connects to the McpServer.
///   2. MCP tools are discovered via <see cref="McpClient.ListToolsAsync"/>.
///   3. <see cref="McpClientTool"/> inherits from <see cref="AIFunction"/>, so tools can be
///      passed directly into <see cref="SessionConfig.Tools"/>.
///   4. When Copilot decides to call a tool, the SDK invokes <c>AIFunction.InvokeAsync</c>
///      on the <see cref="McpClientTool"/>, which forwards the call to the MCP server.
///
/// This demonstrates a clean separation of concerns:
///   - Copilot SDK handles the conversation and orchestration loop.
///   - The MCP server handles the tool implementations.
/// </summary>
public class McpCopilotService : IAsyncDisposable
{
    private readonly CopilotClient _copilotClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _mcpEndpoint;
    private readonly ILogger<McpCopilotService> _logger;

    private McpClient? _mcpClient;
    private IList<McpClientTool> _mcpTools = [];
    private readonly ConcurrentDictionary<string, CopilotSession> _sessions = new();

    public McpCopilotService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<McpCopilotService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // The base URL is injected by Aspire service discovery.
        // Falls back to localhost for standalone development.
        var baseUrl = configuration.GetValue<string>("McpServer:BaseUrl") ?? "http://localhost:5100";
        _mcpEndpoint = $"{baseUrl.TrimEnd('/')}/mcp";

        _copilotClient = new CopilotClient(new CopilotClientOptions
        {
            LogLevel = configuration.GetValue<string>("CopilotSDK:LogLevel", "info"),
            Logger = logger
        });
    }

    public async Task InitializeAsync()
    {
        await _copilotClient.StartAsync();

        // Connect to the MCP server using the Streamable HTTP transport.
        // Service discovery resolves "mcp-server" hostname to the actual address.
        var httpClient = _httpClientFactory.CreateClient("mcp-server");
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(_mcpEndpoint) },
            httpClient);

        _mcpClient = await McpClient.CreateAsync(transport);
        _mcpTools = await _mcpClient.ListToolsAsync();

        _logger.LogInformation(
            "Connected to MCP server at {Endpoint}. Discovered {Count} tool(s): {Names}",
            _mcpEndpoint,
            _mcpTools.Count,
            string.Join(", ", _mcpTools.Select(t => t.Name)));
    }

    /// <summary>Returns metadata about discovered MCP tools for the /tools endpoint.</summary>
    public IReadOnlyList<(string Name, string? Description)> GetToolDescriptions() =>
        _mcpTools.Select(t => (t.Name, (string?)t.Description)).ToList();

    public string McpEndpoint => _mcpEndpoint;

    /// <summary>
    /// Creates a Copilot session pre-loaded with all MCP tools.
    /// Copilot will invoke them automatically during the conversation.
    /// </summary>
    public async Task<(string SessionId, IReadOnlyList<string> ToolNames)> CreateSessionAsync(
        string model,
        string? sessionId = null)
    {
        var session = await _copilotClient.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SessionId = sessionId,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            // McpClientTool inherits AIFunction — pass directly as tools
            Tools = [.. _mcpTools]
        });

        _sessions[session.SessionId] = session;
        var toolNames = _mcpTools.Select(t => t.Name).ToList();

        _logger.LogInformation(
            "Created MCP session {SessionId} with {Count} tool(s)",
            session.SessionId, toolNames.Count);

        return (session.SessionId, toolNames);
    }

    public bool SessionExists(string sessionId) => _sessions.ContainsKey(sessionId);

    public async Task<string> SendMessageAsync(
        string sessionId,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new KeyNotFoundException($"MCP session '{sessionId}' not found.");

        var responseContent = string.Empty;
        var tcs = new TaskCompletionSource<string>();
        using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        var subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    responseContent = msg.Data.Content ?? string.Empty;
                    break;
                case SessionIdleEvent:
                    tcs.TrySetResult(responseContent);
                    break;
                case SessionErrorEvent err:
                    tcs.TrySetException(new InvalidOperationException(err.Data.Message));
                    break;
            }
        });

        try
        {
            await session.SendAsync(new MessageOptions { Prompt = prompt });
            return await tcs.Task;
        }
        finally
        {
            subscription.Dispose();
        }
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var session)) return false;
        await session.DisposeAsync();
        await _copilotClient.DeleteSessionAsync(sessionId);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
            await session.DisposeAsync();
        _sessions.Clear();

        if (_mcpClient is not null)
            await _mcpClient.DisposeAsync();

        await _copilotClient.StopAsync();
    }
}

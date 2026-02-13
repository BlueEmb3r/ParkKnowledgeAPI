using System.ComponentModel;
using System.IO.Pipelines;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ParkKnowledgeAPI.Services.Interfaces;

namespace ParkKnowledgeAPI.Mcp;

/// <summary>
/// Hosts an in-process MCP server and client connected via memory pipes.
/// The agent reads <see cref="Tools"/> to import MCP tools into Semantic Kernel.
/// </summary>
public class McpParkServer : IHostedService, IAsyncDisposable
{
    private readonly IVectorStoreService _vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<McpParkServer> _logger;

    private McpServer? _server;
    private McpClient? _client;
    private Task? _serverTask;

    public IReadOnlyList<McpClientTool> Tools { get; private set; } = [];

    public McpParkServer(
        IVectorStoreService vectorStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<McpParkServer> logger)
    {
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Two pipes form a bidirectional channel — streams are crossed so each
        // side reads what the other writes (like a network socket, but in-memory)
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();

        // Create the search_parks tool as a delegate that closes over DI services
        var searchTool = McpServerTool.Create(
            [Description("Search national park information by natural language query. Returns relevant park details including name, code, state, and content.")]
            async (
                [Description("The search query about national parks")] string query,
                CancellationToken ct) =>
                await ParkTools.SearchParksAsync(_vectorStore, _embeddingGenerator, query, ct),
            new McpServerToolCreateOptions { Name = "search_parks" });

        // Server reads from clientToServer, writes to serverToClient
        _server = McpServer.Create(
            new StreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream()),
            new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "ParkKnowledgeAPI", Version = "1.0.0" },
                Capabilities = new ServerCapabilities { Tools = new ToolsCapability() },
                ToolCollection = [searchTool]
            });

        // Fire-and-forget — server message loop runs in background until disposed
        _serverTask = _server.RunAsync(cancellationToken);

        // Client connects to the other ends of the same pipes
        _client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServerPipe.Writer.AsStream(),
                serverToClientPipe.Reader.AsStream()),
            cancellationToken: cancellationToken);

        // Discover tools from server — these get imported into Semantic Kernel by the agent
        var tools = await _client.ListToolsAsync(cancellationToken: cancellationToken);
        Tools = tools.ToList().AsReadOnly();

        _logger.LogInformation("MCP server started with {ToolCount} tools: {Tools}",
            Tools.Count, string.Join(", ", Tools.Select(t => t.Name)));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        if (_server is not null)
        {
            await _server.DisposeAsync();
            _server = null;
        }

        if (_serverTask is not null)
        {
            try { await _serverTask; } catch (OperationCanceledException) { }
            _serverTask = null;
        }

        Tools = [];
        GC.SuppressFinalize(this);
    }
}

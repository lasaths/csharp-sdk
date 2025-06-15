using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using ModelContextProtocol.HttpListener;
using ModelContextProtocol.Protocol;

namespace McpGrasshopperPlugin;

/// <summary>
/// Hosts an MCP server for Grasshopper using <see cref="HttpListenerMcpTransport"/>.
/// </summary>
public sealed class GrasshopperMcpHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly HttpListenerMcpTransport _transport;

    /// <summary>
    /// Initializes the host for the specified <paramref name="prefix"/>.
    /// </summary>
    public GrasshopperMcpHost(string prefix)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMcpServer();
        builder.WithTools<GrasshopperMCP.Tools.ComponentToolHandler>();
        builder.WithTools<GH_MCP.Tools.ConnectionToolHandler>();
        builder.WithTools<GrasshopperMCP.Tools.DocumentToolHandler>();
        builder.WithTools<GrasshopperMCP.Tools.GeometryToolHandler>();
        builder.WithTools<GH_MCP.Tools.IntentToolHandler>();
        _services = services.BuildServiceProvider();
        var options = _services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var loggerFactory = _services.GetService<ILoggerFactory>();
        _transport = new HttpListenerMcpTransport(prefix, options, loggerFactory, _services);
    }

    /// <summary>Occurs when an MCP message is received.</summary>
    public event Action<JsonRpcMessage>? MessageReceived
    {
        add => _transport.MessageReceived += value;
        remove => _transport.MessageReceived -= value;
    }

    /// <summary>Starts listening for requests.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default) => _transport.StartAsync(cancellationToken);

    /// <summary>Stops listening for requests.</summary>
    public Task StopAsync() => _transport.StopAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _transport.DisposeAsync();
        await _services.DisposeAsync();
    }
}

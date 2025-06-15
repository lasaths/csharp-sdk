namespace ModelContextProtocol.HttpListener;

/// <summary>
/// Defines basic lifecycle control for an MCP server transport.
/// </summary>
public interface IServerTransport : IAsyncDisposable
{
    /// <summary>Starts accepting HTTP requests.</summary>
    /// <param name="cancellationToken">Token to monitor for cancellation.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops listening and releases all resources.</summary>
    Task StopAsync();
}

using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Collections.Concurrent;

namespace ModelContextProtocol.HttpListener;

/// <summary>
/// Minimal HTTP listener based MCP server using SSE and JSON POST endpoints.
/// </summary>
/// <summary>
/// HTTP transport for the MCP server built on <see cref="System.Net.HttpListener"/>.
/// </summary>
public sealed class HttpListenerMcpTransport : IServerTransport
{
    private readonly System.Net.HttpListener _listener = new();
    private readonly McpServerOptions _options;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IServiceProvider? _serviceProvider;
    private readonly string _basePath;
    private readonly string _streamPath;
    private readonly string _messagePath;
    private readonly ConcurrentDictionary<string, HttpSession> _sessions = new();
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>
    /// Occurs whenever a JSON-RPC message is received via the <c>/mcp/message</c> endpoint.
    /// </summary>
    public event Action<JsonRpcMessage>? MessageReceived;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpListenerMcpTransport"/> class.
    /// </summary>
    /// <param name="prefix">The HTTP prefix that the listener should bind to. Must end with a '/'.</param>
    /// <param name="options">Options used when creating <see cref="IMcpServer"/> instances.</param>
    /// <param name="loggerFactory">Optional logger factory for the MCP server.</param>
    public HttpListenerMcpTransport(string prefix, McpServerOptions options, ILoggerFactory? loggerFactory = null, IServiceProvider? serviceProvider = null)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _listener.Prefixes.Add(prefix);

        var uri = new Uri(prefix);
        _basePath = uri.AbsolutePath.TrimEnd('/') + "/";
        _streamPath = _basePath + "stream";
        _messagePath = _basePath + "message";
    }

    /// <summary>
    /// Begins listening for incoming HTTP requests.
    /// </summary>
    /// <param name="cancellationToken">Token used to stop the listener.</param>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
            throw new InvalidOperationException("Server already started");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener.Start();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
#if NETSTANDARD2_0
                context = await _listener.GetContextAsync();
                if (token.IsCancellationRequested)
                {
                    break;
                }
#else
                context = await _listener.GetContextAsync().WaitAsync(token);
#endif
            }
            catch (OperationCanceledException)
            {
                break;
            }
            _ = Task.Run(() => HandleContextAsync(context, token));
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken token)
    {
        string path = context.Request.Url?.AbsolutePath ?? "/";
        if (path.Equals(_streamPath, StringComparison.OrdinalIgnoreCase) && context.Request.HttpMethod == "GET")
        {
            await HandleSseAsync(context, token);
            return;
        }
        if (path.Equals(_messagePath, StringComparison.OrdinalIgnoreCase) && context.Request.HttpMethod == "POST")
        {
            await HandleMessageAsync(context, token);
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        context.Response.Close();
    }

    private async Task HandleSseAsync(HttpListenerContext context, CancellationToken token)
    {
        string sessionId = Guid.NewGuid().ToString("N");
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["mcp-session-id"] = sessionId;

        await using var transport = new SseResponseStreamTransport(context.Response.OutputStream, $"{_messagePath}?sessionId={sessionId}", sessionId);
        await using var server = McpServerFactory.Create(transport, _options, _loggerFactory, _serviceProvider);
        var session = new HttpSession(sessionId, transport, server);
        if (!_sessions.TryAdd(sessionId, session))
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.Close();
            return;
        }

        try
        {
            var transportTask = transport.RunAsync(token);
            var serverTask = server.RunAsync(token);
            await Task.WhenAll(transportTask, serverTask);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            context.Response.Close();
        }
    }

    private async Task HandleMessageAsync(HttpListenerContext context, CancellationToken token)
    {
        string? sessionId = context.Request.QueryString["sessionId"];
        if (sessionId is null || !_sessions.TryGetValue(sessionId, out var session))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.Close();
            return;
        }

        using var doc = await JsonDocument.ParseAsync(context.Request.InputStream, cancellationToken: token);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.Deserialize<JsonRpcMessage>(McpJsonUtilities.DefaultOptions) is { } msg)
                {
                    MessageReceived?.Invoke(msg);
                    await session.Transport.OnMessageReceivedAsync(msg, token);
                }
            }
        }
        else
        {
            if (doc.RootElement.Deserialize<JsonRpcMessage>(McpJsonUtilities.DefaultOptions) is { } msg)
            {
                MessageReceived?.Invoke(msg);
                await session.Transport.OnMessageReceivedAsync(msg, token);
            }
        }
        context.Response.StatusCode = (int)HttpStatusCode.Accepted;
        context.Response.Close();
    }

    /// <summary>
    /// Stops listening for requests and releases all resources.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _listener.Stop();
            if (_listenTask is not null)
            {
                await _listenTask.ConfigureAwait(false);
            }
            _cts.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Disposes the transport and any active sessions.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);

        foreach (var session in _sessions.Values)
        {
            await session.DisposeAsync();
        }
        _sessions.Clear();
    }

    private sealed class HttpSession : IAsyncDisposable
    {
        public string Id { get; }
        public SseResponseStreamTransport Transport { get; }
        public IMcpServer Server { get; }

        public HttpSession(string id, SseResponseStreamTransport transport, IMcpServer server)
        {
            Id = id;
            Transport = transport;
            Server = server;
        }

        public ValueTask DisposeAsync()
        {
            return Server.DisposeAsync();
        }
    }
}

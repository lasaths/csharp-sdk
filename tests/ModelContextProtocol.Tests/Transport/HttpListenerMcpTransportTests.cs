using ModelContextProtocol.HttpListener;
using ModelContextProtocol.Server;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Tests.Utils;
using System.Net;
using System.Text.Json;

namespace ModelContextProtocol.Tests.Transport;

public class HttpListenerMcpTransportTests(ITestOutputHelper outputHelper) : LoggedTest(outputHelper)
{
    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static McpServerOptions CreateServerOptions()
    {
        return new McpServerOptions
        {
            Capabilities = new ServerCapabilities
            {
                Tools = new()
                {
                    ListToolsHandler = async (_, _) => new ListToolsResult
                    {
                        Tools =
                        [
                            new Tool
                            {
                                Name = "echo",
                                Description = "Echoes the input back to the client.",
                                InputSchema = JsonSerializer.Deserialize<JsonElement>("""
                                    {
                                        "type": "object",
                                        "properties": {
                                            "message": {"type": "string"}
                                        },
                                        "required": ["message"]
                                    }
                                """, McpJsonUtilities.DefaultOptions),
                            },
                            new Tool
                            {
                                Name = "echoSessionId",
                                Description = "Returns the current session id.",
                                InputSchema = JsonSerializer.Deserialize<JsonElement>("""
                                    {"type": "object"}
                                """, McpJsonUtilities.DefaultOptions),
                            }
                        ]
                    },
                    CallToolHandler = async (request, _) =>
                    {
                        if (request.Params is null)
                            throw new McpException("Missing params", McpErrorCode.InvalidParams);

                        return request.Params.Name switch
                        {
                            "echo" when request.Params.Arguments?.TryGetValue("message", out var msg) == true
                                => new CallToolResponse { Content = [ new Content { Type = "text", Text = $"Echo: {msg}" } ] },
                            "echoSessionId" => new CallToolResponse { Content = [ new Content { Type = "text", Text = request.Server.SessionId } ] },
                            _ => throw new McpException("Invalid tool", McpErrorCode.InvalidParams)
                        };
                    }
                }
            },
            ServerInfo = new Implementation { Name = "HttpListenerTest", Version = "1.0" },
        };
    }

    private async Task<(HttpListenerMcpTransport transport, IMcpClient client)> StartServerAndClientAsync()
    {
        int port = GetFreePort();
        var options = CreateServerOptions();
        var prefix = $"http://localhost:{port}/mcp/";
        var transport = new HttpListenerMcpTransport(prefix, options, LoggerFactory);
        await transport.StartAsync(TestContext.Current.CancellationToken);

        var clientOptions = new SseClientTransportOptions
        {
            Endpoint = new Uri($"http://localhost:{port}/mcp/stream"),
            Name = "HttpListenerClient",
            TransportMode = HttpTransportMode.Sse,
        };
        var client = await McpClientFactory.CreateAsync(
            new SseClientTransport(clientOptions),
            loggerFactory: LoggerFactory,
            cancellationToken: TestContext.Current.CancellationToken);

        return (transport, client);
    }

    [Fact]
    public async Task ConnectAndPing()
    {
        var (transport, client) = await StartServerAndClientAsync();
        await using var t = transport;
        await using var c = client;

        await c.PingAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EchoTool_ReturnsEcho()
    {
        var (transport, client) = await StartServerAndClientAsync();
        await using var t = transport;
        await using var c = client;

        var result = await c.CallToolAsync(
            "echo",
            new Dictionary<string, object?> { ["message"] = "hello" },
            cancellationToken: TestContext.Current.CancellationToken);

        var content = Assert.Single(result.Content);
        Assert.Equal("text", content.Type);
        Assert.Equal("Echo: hello", content.Text);
    }
}

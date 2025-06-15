# HttpListener MCP Transport

This library provides an `HttpListener` based implementation of the MCP server transport.
It targets **.NET Standard 2.0** so it can be used from applications running on .NET Framework 4.8.
The listener exposes two endpoints relative to the configured prefix:

* `GET {prefix}stream` - Server-Sent Events stream of responses
* `POST {prefix}message` - Accepts JSON-RPC messages

```csharp
var transport = new HttpListenerMcpTransport("http://localhost:5000/mcp/", new McpServerOptions());
transport.MessageReceived += msg => Console.WriteLine(msg.Method);
await transport.StartAsync();
```

A Grasshopper component can use the transport as follows:

```csharp
var options = new McpServerOptions();
listener = new HttpListenerMcpTransport(prefix, options);
listener.MessageReceived += msg =>
{
    messages.Add(JsonSerializer.Serialize(msg, McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonRpcMessage))));
    ExpireSolution(true);
};
await listener.StartAsync();
```

using ModelContextProtocol.Client;
using OpenAI;

// Simple client that connects to an MCP server using the HttpListener transport.
// The server is expected to expose the /mcp/stream and /mcp/message endpoints.

var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var transportOptions = new SseClientTransportOptions
{
    Endpoint = new Uri("http://localhost:5000/mcp/stream"),
    Name = "HttpListenerServer",
    TransportMode = HttpTransportMode.Sse,
};

await using var client = await McpClientFactory.CreateAsync(new SseClientTransport(transportOptions));

Console.WriteLine("Connected to server. Available tools:");
var tools = await client.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"- {tool.Name}");
}

Console.WriteLine();
Console.WriteLine("Type a message to send to the echo tool (empty line to quit):");
string? line;
while (!string.IsNullOrEmpty(line = Console.ReadLine()))
{
    var result = await client.CallToolAsync("echo", new Dictionary<string, object?> { ["message"] = line });
    Console.WriteLine(string.Join("\n", result.Content.Select(c => c.Text)));
    Console.WriteLine();
}

if (!string.IsNullOrEmpty(openAiKey))
{
    // Example of creating an OpenAI chat client using the secret key
    var chatClient = new OpenAIClient(openAiKey).GetChatClient("gpt-4o-mini");
    Console.WriteLine($"OpenAI client created for model {chatClient.Model}");
}

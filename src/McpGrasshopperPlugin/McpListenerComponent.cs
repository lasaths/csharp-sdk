using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace McpGrasshopperPlugin;

/// <summary>
/// Example Grasshopper component that starts <see cref="HttpListenerMcpTransport"/>
/// and outputs received JSON-RPC messages and log lines.
/// </summary>

public class McpListenerComponent : GH_Component
{
    private GrasshopperMcpHost? _server;
    private CancellationTokenSource? _cts;
    private readonly List<string> _logs = new();
    private readonly List<string> _messages = new();

    public McpListenerComponent() : base("MCP Listener", "MCP", "Start MCP server", "MCP", "Server") { }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddBooleanParameter("On", "On", "Start server", GH_ParamAccess.item, false);
        pManager.AddTextParameter("Prefix", "P", "HTTP prefix", GH_ParamAccess.item, "http://localhost:3001/mcp/");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Messages", "M", "Received JSON-RPC messages", GH_ParamAccess.list);
        pManager.AddTextParameter("Logs", "L", "Log messages", GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        bool on = false;
        string prefix = "";
        if (!da.GetData(0, ref on)) return;
        if (!da.GetData(1, ref prefix)) return;

        if (on && _server == null)
        {
            _server = new GrasshopperMcpHost(prefix);
            _server.MessageReceived += OnMessage;
            _cts = new CancellationTokenSource();
            _server.StartAsync(_cts.Token).Wait();
            _logs.Add($"Server started at {prefix}");
        }
        else if (!on && _server != null)
        {
            _cts?.Cancel();
            _server.StopAsync().Wait();
            _server.DisposeAsync().AsTask().Wait();
            _server.MessageReceived -= OnMessage;
            _server = null;
            _cts?.Dispose();
            _cts = null;
            _logs.Add("Server stopped");
        }

        da.SetDataList(0, _messages);
        da.SetDataList(1, _logs);
    }

    private void OnMessage(JsonRpcMessage msg)
    {
        _messages.Add(JsonSerializer.Serialize(msg, McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonRpcMessage))));
        ExpireSolution(true);
    }

    public override Guid ComponentGuid => new("6b8ec8b1-599f-4d5a-8244-e2d7f0a2ea76");
}

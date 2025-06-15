using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GrasshopperMCP.Models;
using GrasshopperMCP.Tools;
using GH_MCP.Models;
using GH_MCP.Utils;
using Rhino;
using Newtonsoft.Json;

namespace GH_MCP.Tools
{
    /// <summary>
    /// </summary>
    public class IntentToolHandler
    {
        private static Dictionary<string, string> _componentIdMap = new Dictionary<string, string>();

        /// <summary>
        /// </summary>
        public static object CreatePattern(ToolRequest tool)
        {
            if (!tool.Parameters.TryGetValue("description", out object descriptionObj) || descriptionObj == null)
            {
                return Response.CreateError("Missing required parameter: description");
            }
            string description = descriptionObj.ToString();

            string patternName = IntentRecognizer.RecognizeIntent(description);
            if (string.IsNullOrEmpty(patternName))
            {
                return Response.CreateError($"Could not recognize intent from description: {description}");
            }

            RhinoApp.WriteLine($"Recognized intent: {patternName}");

            var (components, connections) = IntentRecognizer.GetPatternDetails(patternName);
            if (components.Count == 0)
            {
                return Response.CreateError($"Pattern '{patternName}' has no components defined");
            }

            _componentIdMap.Clear();

            foreach (var component in components)
            {
                try
                {
                    var addToolRequest = new ToolRequest(
                        "add_component",
                        new Dictionary<string, object>
                        {
                            { "type", component.Type },
                            { "x", component.X },
                            { "y", component.Y }
                        }
                    );

                    if (component.Settings != null)
                    {
                        foreach (var setting in component.Settings)
                        {
                            addToolRequest.Parameters.Add(setting.Key, setting.Value);
                        }
                    }

                    var result = ComponentToolHandler.AddComponent(addToolRequest);
                    if (result is Response response && response.Success && response.Data != null)
                    {
                        string componentId = response.Data.ToString();
                        _componentIdMap[component.Id] = componentId;
                        RhinoApp.WriteLine($"Created component {component.Type} with ID {componentId}");
                    }
                    else
                    {
                        RhinoApp.WriteLine($"Failed to create component {component.Type}");
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error creating component {component.Type}: {ex.Message}");
                }

                Thread.Sleep(100);
            }

            foreach (var connection in connections)
            {
                try
                {
                    if (!_componentIdMap.TryGetValue(connection.SourceId, out string sourceId) ||
                        !_componentIdMap.TryGetValue(connection.TargetId, out string targetId))
                    {
                        RhinoApp.WriteLine($"Could not find component IDs for connection {connection.SourceId} -> {connection.TargetId}");
                        continue;
                    }

                    var connectToolRequest = new ToolRequest(
                        "connect_components",
                        new Dictionary<string, object>
                        {
                            { "sourceId", sourceId },
                            { "sourceParam", connection.SourceParam },
                            { "targetId", targetId },
                            { "targetParam", connection.TargetParam }
                        }
                    );

                    var result = ConnectionToolHandler.ConnectComponents(connectToolRequest);
                    if (result is Response response && response.Success)
                    {
                        RhinoApp.WriteLine($"Connected {connection.SourceId}.{connection.SourceParam} -> {connection.TargetId}.{connection.TargetParam}");
                    }
                    else
                    {
                        RhinoApp.WriteLine($"Failed to connect {connection.SourceId}.{connection.SourceParam} -> {connection.TargetId}.{connection.TargetParam}");
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error creating connection: {ex.Message}");
                }

                Thread.Sleep(100);
            }

            return Response.Ok(new
            {
                Pattern = patternName,
                ComponentCount = components.Count,
                ConnectionCount = connections.Count
            });
        }

        /// <summary>
        /// </summary>
        public static object GetAvailablePatterns(ToolRequest tool)
        {
            IntentRecognizer.Initialize();

            var patterns = new List<string>();
            if (tool.Parameters.TryGetValue("query", out object queryObj) && queryObj != null)
            {
                string query = queryObj.ToString();
                string patternName = IntentRecognizer.RecognizeIntent(query);
                if (!string.IsNullOrEmpty(patternName))
                {
                    patterns.Add(patternName);
                }
            }
            else
            {
            }

            return Response.Ok(patterns);
        }
    }
}

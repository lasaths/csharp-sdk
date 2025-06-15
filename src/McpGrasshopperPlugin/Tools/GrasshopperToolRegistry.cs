using System;
using System.Collections.Generic;
using GH_MCP.Tools;
using GrasshopperMCP.Models;
using GrasshopperMCP.Tools;
using Grasshopper.Kernel;
using Rhino;
using System.Linq;

namespace GH_MCP.Tools
{
    /// <summary>
    /// Registry for Grasshopper tools. Provides registration and execution
    /// helpers used by the MCP host.
    /// </summary>
    public static class GrasshopperToolRegistry
    {
        // Maps tool names to their handler delegates.
        private static readonly Dictionary<string, Func<ToolRequest, object>> ToolHandlers = new();

        /// <summary>
        /// Initializes the registry and registers all builtin tools.
        /// </summary>
        public static void Initialize()
        {
            // Register geometry tools
            RegisterGeometryTools();
            
            // Register component tools
            RegisterComponentTools();
            
            // Register document tools
            RegisterDocumentTools();
            
            // Register intent tools
            RegisterIntentTools();
            
            RhinoApp.WriteLine("GH_MCP: Tool registry initialized.");
        }

        /// <summary>
        /// Registers geometry-related tools
        /// </summary>
        private static void RegisterGeometryTools()
        {
            // Create point
            RegisterTool("create_point", GeometryToolHandler.CreatePoint);
            
            // Create curve
            RegisterTool("create_curve", GeometryToolHandler.CreateCurve);
            
            // Create circle
            RegisterTool("create_circle", GeometryToolHandler.CreateCircle);
        }

        /// <summary>
        /// Registers component manipulation tools
        /// </summary>
        private static void RegisterComponentTools()
        {
            // Add component
            RegisterTool("add_component", ComponentToolHandler.AddComponent);
            
            // Connect components
            RegisterTool("connect_components", ConnectionToolHandler.ConnectComponents);
            
            // Set component value
            RegisterTool("set_component_value", ComponentToolHandler.SetComponentValue);
            
            // Get component information
            RegisterTool("get_component_info", ComponentToolHandler.GetComponentInfo);
        }

        /// <summary>
        /// Registers document-related tools
        /// </summary>
        private static void RegisterDocumentTools()
        {
            // Get document info
            RegisterTool("get_document_info", DocumentToolHandler.GetDocumentInfo);
            
            // Clear document
            RegisterTool("clear_document", DocumentToolHandler.ClearDocument);
            
            // Save document
            RegisterTool("save_document", DocumentToolHandler.SaveDocument);
            
            // Load document
            RegisterTool("load_document", DocumentToolHandler.LoadDocument);
        }

        /// <summary>
        /// Registers intent tools
        /// </summary>
        private static void RegisterIntentTools()
        {
            // Create pattern
            RegisterTool("create_pattern", IntentToolHandler.CreatePattern);
            
            // Get available patterns
            RegisterTool("get_available_patterns", IntentToolHandler.GetAvailablePatterns);
            
            RhinoApp.WriteLine("GH_MCP: Intent tools registered.");
        }

        /// <summary>
        /// </summary>
        /// <param name="toolType">Tool name</param>
        /// <param name="handler">Handler delegate</param>
        public static void RegisterTool(string toolType, Func<ToolRequest, object> handler)
        {
            if (string.IsNullOrEmpty(toolType))
                throw new ArgumentNullException(nameof(toolType));
                
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
                
            ToolHandlers[toolType] = handler;
            RhinoApp.WriteLine($"GH_MCP: Registered tool handler for '{toolType}'");
        }

        /// <summary>
        /// </summary>
        /// <param name="tool">The tool to execute.</param>
        /// <returns>Tool execution result.</returns>
        public static Response ExecuteTool(ToolRequest tool)
        {
            if (tool == null)
            {
                return Response.CreateError("Tool is null");
            }

            if (string.IsNullOrEmpty(tool.Type))
            {
                return Response.CreateError("Tool type is null or empty");
            }

            if (ToolHandlers.TryGetValue(tool.Type, out var handler))
            {
                try
                {
                    var result = handler(tool);
                    return Response.Ok(result);
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"GH_MCP: Error executing tool '{tool.Type}': {ex.Message}");
                    return Response.CreateError($"Error executing tool '{tool.Type}': {ex.Message}");
                }
            }

            return Response.CreateError($"No handler registered for tool type '{tool.Type}'");
        }

        /// <summary>
        /// </summary>
        public static List<string> GetRegisteredToolTypes()
        {
            return ToolHandlers.Keys.ToList();
        }
    }
}

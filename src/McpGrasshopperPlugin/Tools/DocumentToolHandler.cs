using System;
using System.Collections.Generic;
using System.IO;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Rhino;
using System.Linq;
using System.Threading;

namespace GrasshopperMCP.Tools
{
    /// <summary>
    /// </summary>
    public static class DocumentToolHandler
    {
        /// <summary>
        /// </summary>
        public static object GetDocumentInfo(ToolRequest tool)
        {
            object result = null;
            Exception exception = null;
            
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }
                    
                    var components = new List<object>();
                    foreach (var obj in doc.Objects)
                    {
                        var componentInfo = new Dictionary<string, object>
                        {
                            { "id", obj.InstanceGuid.ToString() },
                            { "type", obj.GetType().Name },
                            { "name", obj.NickName }
                        };
                        
                        components.Add(componentInfo);
                    }
                    
                    var docInfo = new Dictionary<string, object>
                    {
                        { "name", doc.DisplayName },
                        { "path", doc.FilePath },
                        { "componentCount", doc.Objects.Count },
                        { "components", components }
                    };
                    
                    result = docInfo;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in GetDocumentInfo: {ex.Message}");
                }
            }));
            
            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }
            
            if (exception != null)
            {
                throw exception;
            }
            
            return result;
        }
        
        /// <summary>
        /// </summary>
        public static object ClearDocument(ToolRequest tool)
        {
            object result = null;
            Exception exception = null;
            
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }
                    
                    var objectsToRemove = doc.Objects.ToList();
                    
                    var essentialComponents = objectsToRemove.Where(obj => 
                        obj.NickName.Contains("MCP") || 
                        obj.NickName.Contains("Claude") ||
                        obj.GetType().Name.Contains("GH_MCP") ||
                        obj.Description.Contains("Machine Control Protocol") ||
                        obj.GetType().Name.Contains("GH_BooleanToggle") ||
                        obj.GetType().Name.Contains("GH_Panel") ||
                        obj.NickName.Contains("Toggle") ||
                        obj.NickName.Contains("Status") ||
                        obj.NickName.Contains("Panel")
                    ).ToList();
                    
                    foreach (var component in essentialComponents)
                    {
                        objectsToRemove.Remove(component);
                    }
                    
                    doc.RemoveObjects(objectsToRemove, false);
                    
                    doc.NewSolution(false);
                    
                    result = new
                    {
                        success = true,
                        message = "Document cleared"
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in ClearDocument: {ex.Message}");
                }
            }));
            
            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }
            
            if (exception != null)
            {
                throw exception;
            }
            
            return result;
        }
        
        /// <summary>
        /// </summary>
        public static object SaveDocument(ToolRequest tool)
        {
            string path = tool.GetParameter<string>("path");
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Save path is required");
            }
            
            return new
            {
                success = false,
                message = "SaveDocument is temporarily disabled due to API compatibility issues. Please save the document manually."
            };
        }
        
        /// <summary>
        /// </summary>
        public static object LoadDocument(ToolRequest tool)
        {
            string path = tool.GetParameter<string>("path");
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Load path is required");
            }
            
            return new
            {
                success = false,
                message = "LoadDocument is temporarily disabled due to API compatibility issues. Please load the document manually."
            };
        }
    }
}

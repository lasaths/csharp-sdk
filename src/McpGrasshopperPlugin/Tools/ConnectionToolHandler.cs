using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GrasshopperMCP.Models;
using GH_MCP.Models;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino;
using Newtonsoft.Json;
using GH_MCP.Utils;

namespace GH_MCP.Tools
{
    /// <summary>
    /// </summary>
    public class ConnectionToolHandler
    {
        /// <summary>
        /// </summary>
        public static object ConnectComponents(ToolRequest tool)
        {
            if (!tool.Parameters.TryGetValue("sourceId", out object sourceIdObj) || sourceIdObj == null)
            {
                return Response.CreateError("Missing required parameter: sourceId");
            }
            string sourceId = sourceIdObj.ToString();

            string sourceParam = null;
            int? sourceParamIndex = null;
            if (tool.Parameters.TryGetValue("sourceParam", out object sourceParamObj) && sourceParamObj != null)
            {
                sourceParam = sourceParamObj.ToString();
                sourceParam = FuzzyMatcher.GetClosestParameterName(sourceParam);
            }
            else if (tool.Parameters.TryGetValue("sourceParamIndex", out object sourceParamIndexObj) && sourceParamIndexObj != null)
            {
                if (int.TryParse(sourceParamIndexObj.ToString(), out int index))
                {
                    sourceParamIndex = index;
                }
            }

            if (!tool.Parameters.TryGetValue("targetId", out object targetIdObj) || targetIdObj == null)
            {
                return Response.CreateError("Missing required parameter: targetId");
            }
            string targetId = targetIdObj.ToString();

            string targetParam = null;
            int? targetParamIndex = null;
            if (tool.Parameters.TryGetValue("targetParam", out object targetParamObj) && targetParamObj != null)
            {
                targetParam = targetParamObj.ToString();
                targetParam = FuzzyMatcher.GetClosestParameterName(targetParam);
            }
            else if (tool.Parameters.TryGetValue("targetParamIndex", out object targetParamIndexObj) && targetParamIndexObj != null)
            {
                if (int.TryParse(targetParamIndexObj.ToString(), out int index))
                {
                    targetParamIndex = index;
                }
            }

            RhinoApp.WriteLine($"Connecting: sourceId={sourceId}, sourceParam={sourceParam}, targetId={targetId}, targetParam={targetParam}");

            var connection = new ConnectionPairing
            {
                Source = new Connection
                {
                    ComponentId = sourceId,
                    ParameterName = sourceParam,
                    ParameterIndex = sourceParamIndex
                },
                Target = new Connection
                {
                    ComponentId = targetId,
                    ParameterName = targetParam,
                    ParameterIndex = targetParamIndex
                }
            };

            if (!connection.IsValid())
            {
                return Response.CreateError("Invalid connection parameters");
            }

            object result = null;
            Exception exception = null;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        exception = new InvalidOperationException("No active Grasshopper document");
                        return;
                    }

                    Guid sourceGuid;
                    if (!Guid.TryParse(connection.Source.ComponentId, out sourceGuid))
                    {
                        exception = new ArgumentException($"Invalid source component ID: {connection.Source.ComponentId}");
                        return;
                    }

                    var sourceComponent = doc.FindObject(sourceGuid, true);
                    if (sourceComponent == null)
                    {
                        exception = new ArgumentException($"Source component not found: {connection.Source.ComponentId}");
                        return;
                    }

                    Guid targetGuid;
                    if (!Guid.TryParse(connection.Target.ComponentId, out targetGuid))
                    {
                        exception = new ArgumentException($"Invalid target component ID: {connection.Target.ComponentId}");
                        return;
                    }

                    var targetComponent = doc.FindObject(targetGuid, true);
                    if (targetComponent == null)
                    {
                        exception = new ArgumentException($"Target component not found: {connection.Target.ComponentId}");
                        return;
                    }

                    if (sourceComponent is IGH_Param && ((IGH_Param)sourceComponent).Kind == GH_ParamKind.input)
                    {
                        exception = new ArgumentException("Source component cannot be an input parameter");
                        return;
                    }

                    if (targetComponent is IGH_Param && ((IGH_Param)targetComponent).Kind == GH_ParamKind.output)
                    {
                        exception = new ArgumentException("Target component cannot be an output parameter");
                        return;
                    }

                    IGH_Param sourceParameter = GetParameter(sourceComponent, connection.Source, false);
                    if (sourceParameter == null)
                    {
                        exception = new ArgumentException($"Source parameter not found: {connection.Source.ParameterName ?? connection.Source.ParameterIndex.ToString()}");
                        return;
                    }

                    IGH_Param targetParameter = GetParameter(targetComponent, connection.Target, true);
                    if (targetParameter == null)
                    {
                        exception = new ArgumentException($"Target parameter not found: {connection.Target.ParameterName ?? connection.Target.ParameterIndex.ToString()}");
                        return;
                    }

                    if (!AreParametersCompatible(sourceParameter, targetParameter))
                    {
                        exception = new ArgumentException($"Parameters are not compatible: {sourceParameter.GetType().Name} cannot connect to {targetParameter.GetType().Name}");
                        return;
                    }

                    if (targetParameter.SourceCount > 0)
                    {
                        targetParameter.RemoveAllSources();
                    }

                    targetParameter.AddSource(sourceParameter);
                    
                    targetParameter.CollectData();
                    targetParameter.ComputeData();
                    
                    doc.NewSolution(false);

                    result = new
                    {
                        success = true,
                        message = "Connection created successfully",
                        sourceId = connection.Source.ComponentId,
                        targetId = connection.Target.ComponentId,
                        sourceParam = sourceParameter.Name,
                        targetParam = targetParameter.Name,
                        sourceType = sourceParameter.GetType().Name,
                        targetType = targetParameter.GetType().Name,
                        sourceDescription = sourceParameter.Description,
                        targetDescription = targetParameter.Description
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in ConnectComponents: {ex.Message}");
                }
            }));

            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }

            if (exception != null)
            {
                return Response.CreateError($"Error executing tool 'connect_components': {exception.Message}");
            }

            return Response.Ok(result);
        }

        /// <summary>
        /// </summary>
        private static IGH_Param GetParameter(IGH_DocumentObject docObj, Connection connection, bool isInput)
        {
            if (docObj is IGH_Param param)
            {
                return param;
            }
            
            if (docObj is IGH_Component component)
            {
                IList<IGH_Param> parameters = isInput ? component.Params.Input : component.Params.Output;
                
                if (parameters == null || parameters.Count == 0)
                {
                    return null;
                }
                
                if (parameters.Count == 1 && string.IsNullOrEmpty(connection.ParameterName) && !connection.ParameterIndex.HasValue)
                {
                    return parameters[0];
                }
                
                if (!string.IsNullOrEmpty(connection.ParameterName))
                {
                    foreach (var p in parameters)
                    {
                        if (string.Equals(p.Name, connection.ParameterName, StringComparison.OrdinalIgnoreCase))
                        {
                            return p;
                        }
                    }
                    
                    foreach (var p in parameters)
                    {
                        if (p.Name.IndexOf(connection.ParameterName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return p;
                        }
                    }

                    foreach (var p in parameters)
                    {
                        if (string.Equals(p.NickName, connection.ParameterName, StringComparison.OrdinalIgnoreCase))
                        {
                            return p;
                        }
                    }
                }
                
                if (connection.ParameterIndex.HasValue)
                {
                    int index = connection.ParameterIndex.Value;
                    if (index >= 0 && index < parameters.Count)
                    {
                        return parameters[index];
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// </summary>
        private static bool AreParametersCompatible(IGH_Param source, IGH_Param target)
        {
            if (source.GetType() == target.GetType())
            {
                return true;
            }

            var sourceType = source.Type;
            var targetType = target.Type;
            
            RhinoApp.WriteLine($"Parameter types: source={sourceType.Name}, target={targetType.Name}");
            RhinoApp.WriteLine($"Parameter names: source={source.Name}, target={target.Name}");
            
            bool isSourceNumeric = IsNumericType(source);
            bool isTargetNumeric = IsNumericType(target);
            
            if (isSourceNumeric && isTargetNumeric)
            {
                return true;
            }

            bool isSourceCurve = source is Param_Curve;
            bool isTargetCurve = target is Param_Curve;
            bool isSourceGeometry = source is Param_Geometry;
            bool isTargetGeometry = target is Param_Geometry;

            if ((isSourceCurve && isTargetGeometry) || (isSourceGeometry && isTargetCurve))
            {
                return true;
            }

            bool isSourcePoint = source is Param_Point;
            bool isTargetPoint = target is Param_Point;
            bool isSourceVector = source is Param_Vector;
            bool isTargetVector = target is Param_Vector;

            if ((isSourcePoint && isTargetVector) || (isSourceVector && isTargetPoint))
            {
                return true;
            }

            var sourceDoc = source.OnPingDocument();
            var targetDoc = target.OnPingDocument();
            
            if (sourceDoc != null && targetDoc != null)
            {
                IGH_Component sourceComponent = FindComponentForParam(sourceDoc, source);
                IGH_Component targetComponent = FindComponentForParam(targetDoc, target);
                
                if (sourceComponent != null && targetComponent != null)
                {
                    RhinoApp.WriteLine($"Components: source={sourceComponent.Name}, target={targetComponent.Name}");
                    RhinoApp.WriteLine($"Component GUIDs: source={sourceComponent.ComponentGuid}, target={targetComponent.ComponentGuid}");
                    
                    if (IsPlaneComponent(sourceComponent) && RequiresPlaneInput(targetComponent))
                    {
                        RhinoApp.WriteLine("Connecting plane component to geometry component that requires plane input");
                        return true;
                    }
                    
                    if (sourceComponent.Name.Contains("Number") && targetComponent.Name.Contains("Circle"))
                    {
                        if (targetComponent.ComponentGuid.ToString() == "d1028c72-ff86-4057-9eb0-36c687a4d98c")
                        {
                            RhinoApp.WriteLine("Detected connection to Circle parameter container instead of Circle component");
                            return false;
                        }
                        if (targetComponent.ComponentGuid.ToString() == "807b86e3-be8d-4970-92b5-f8cdcb45b06b")
                        {
                            return true;
                        }
                    }
                    
                    if (IsPlaneComponent(sourceComponent) && targetComponent.Name.Contains("Box"))
                    {
                        RhinoApp.WriteLine("Connecting plane component to box component");
                        return true;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// </summary>
        private static bool IsNumericType(IGH_Param param)
        {
            return param is Param_Integer || 
                   param is Param_Number || 
                   param is Param_Time;
        }

        /// <summary>
        /// </summary>
        private static IGH_Component FindComponentForParam(GH_Document doc, IGH_Param param)
        {
            foreach (var obj in doc.Objects)
            {
                if (obj is IGH_Component comp)
                {
                    foreach (var outParam in comp.Params.Output)
                    {
                        if (outParam.InstanceGuid == param.InstanceGuid)
                        {
                            return comp;
                        }
                    }
                    
                    foreach (var inParam in comp.Params.Input)
                    {
                        if (inParam.InstanceGuid == param.InstanceGuid)
                        {
                            return comp;
                        }
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// </summary>
        private static bool IsPlaneComponent(IGH_Component component)
        {
            if (component == null)
                return false;
                
            string name = component.Name.ToLowerInvariant();
            if (name.Contains("plane"))
                return true;
                
            if (component.ComponentGuid.ToString() == "896a1e5e-c2ac-4996-a6d8-5b61157080b3")
                return true;
                
            return false;
        }
        
        /// <summary>
        /// </summary>
        private static bool RequiresPlaneInput(IGH_Component component)
        {
            if (component == null)
                return false;
                
            foreach (var param in component.Params.Input)
            {
                string paramName = param.Name.ToLowerInvariant();
                if (paramName.Contains("plane") || paramName.Contains("base"))
                    return true;
            }
            
            string name = component.Name.ToLowerInvariant();
            return name.Contains("box") || 
                   name.Contains("rectangle") || 
                   name.Contains("circle") || 
                   name.Contains("cylinder") || 
                   name.Contains("cone");
        }
    }

    public class ConnectionPairing
    {
        public Connection Source { get; set; }
        public Connection Target { get; set; }

        public bool IsValid()
        {
            return Source != null && Target != null;
        }
    }

    public class Connection
    {
        public string ComponentId { get; set; }
        public string ParameterName { get; set; }
        public int? ParameterIndex { get; set; }
    }
}

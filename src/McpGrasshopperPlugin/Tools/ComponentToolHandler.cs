using System;
using System.Collections.Generic;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using System.Linq;
using Grasshopper.Kernel.Components;
using System.Threading;
using GH_MCP.Utils;

namespace GrasshopperMCP.Tools
{
    /// <summary>
    /// </summary>
    public static class ComponentToolHandler
    {
        /// <summary>
        /// </summary>
        public static object AddComponent(ToolRequest tool)
        {
            string type = tool.GetParameter<string>("type");
            double x = tool.GetParameter<double>("x");
            double y = tool.GetParameter<double>("y");
            
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("Component type is required");
            }
            
            string normalizedType = FuzzyMatcher.GetClosestComponentName(type);
            
            RhinoApp.WriteLine($"AddComponent request: type={type}, normalized={normalizedType}, x={x}, y={y}");
            
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
                    
                    IGH_DocumentObject component = null;
                    
                    bool loggedComponentTypes = false;
                    if (!loggedComponentTypes)
                    {
                        var availableTypes = Grasshopper.Instances.ComponentServer.ObjectProxies
                            .Select(p => p.Desc.Name)
                            .OrderBy(n => n)
                            .ToList();
                        
                        RhinoApp.WriteLine($"Available component types: {string.Join(", ", availableTypes.Take(50))}...");
                        loggedComponentTypes = true;
                    }
                    
                    switch (normalizedType.ToLowerInvariant())
                    {
                        case "xy plane":
                            component = CreateComponentByName("XY Plane");
                            break;
                        case "xz plane":
                            component = CreateComponentByName("XZ Plane");
                            break;
                        case "yz plane":
                            component = CreateComponentByName("YZ Plane");
                            break;
                        case "plane 3pt":
                            component = CreateComponentByName("Plane 3Pt");
                            break;
                            
                        case "box":
                            component = CreateComponentByName("Box");
                            break;
                        case "sphere":
                            component = CreateComponentByName("Sphere");
                            break;
                        case "cylinder":
                            component = CreateComponentByName("Cylinder");
                            break;
                        case "cone":
                            component = CreateComponentByName("Cone");
                            break;
                        case "circle":
                            component = CreateComponentByName("Circle");
                            break;
                        case "rectangle":
                            component = CreateComponentByName("Rectangle");
                            break;
                        case "line":
                            component = CreateComponentByName("Line");
                            break;
                            
                        case "point":
                        case "pt":
                        case "pointparam":
                        case "param_point":
                            component = new Param_Point();
                            break;
                        case "curve":
                        case "crv":
                        case "curveparam":
                        case "param_curve":
                            component = new Param_Curve();
                            break;
                        case "circleparam":
                        case "param_circle":
                            component = new Param_Circle();
                            break;
                        case "lineparam":
                        case "param_line":
                            component = new Param_Line();
                            break;
                        case "panel":
                        case "gh_panel":
                            component = new GH_Panel();
                            break;
                        case "slider":
                        case "numberslider":
                        case "gh_numberslider":
                            var slider = new GH_NumberSlider();
                            slider.SetInitCode("0.0 < 0.5 < 1.0");
                            component = slider;
                            break;
                        case "number":
                        case "num":
                        case "integer":
                        case "int":
                        case "param_number":
                        case "param_integer":
                            component = new Param_Number();
                            break;
                        case "construct point":
                        case "constructpoint":
                        case "pt xyz":
                        case "xyz":
                            var pointProxy = Grasshopper.Instances.ComponentServer.ObjectProxies
                                .FirstOrDefault(p => p.Desc.Name.Equals("Construct Point", StringComparison.OrdinalIgnoreCase));
                            if (pointProxy != null)
                            {
                                component = pointProxy.CreateInstance();
                            }
                            else
                            {
                                throw new ArgumentException("Construct Point component not found");
                            }
                            break;
                        default:
                            Guid componentGuid;
                            if (Guid.TryParse(type, out componentGuid))
                            {
                                component = Grasshopper.Instances.ComponentServer.EmitObject(componentGuid);
                                RhinoApp.WriteLine($"Attempting to create component by GUID: {componentGuid}");
                            }
                            
                            if (component == null)
                            {
                                RhinoApp.WriteLine($"Attempting to find component by name: {type}");
                                var obj = Grasshopper.Instances.ComponentServer.ObjectProxies
                                    .FirstOrDefault(p => p.Desc.Name.Equals(type, StringComparison.OrdinalIgnoreCase));
                                    
                                if (obj != null)
                                {
                                    RhinoApp.WriteLine($"Found component: {obj.Desc.Name}");
                                    component = obj.CreateInstance();
                                }
                                else
                                {
                                    RhinoApp.WriteLine($"Attempting to find component by partial name match: {type}");
                                    obj = Grasshopper.Instances.ComponentServer.ObjectProxies
                                        .FirstOrDefault(p => p.Desc.Name.IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0);
                                        
                                    if (obj != null)
                                    {
                                        RhinoApp.WriteLine($"Found component by partial match: {obj.Desc.Name}");
                                        component = obj.CreateInstance();
                                    }
                                }
                            }
                            
                            if (component == null)
                            {
                                var possibleMatches = Grasshopper.Instances.ComponentServer.ObjectProxies
                                    .Where(p => p.Desc.Name.IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0)
                                    .Select(p => p.Desc.Name)
                                    .Take(10)
                                    .ToList();
                                
                                var errorMessage = $"Unknown component type: {type}";
                                if (possibleMatches.Any())
                                {
                                    errorMessage += $". Possible matches: {string.Join(", ", possibleMatches)}";
                                }
                                
                                throw new ArgumentException(errorMessage);
                            }
                            break;
                    }
                    
                    if (component != null)
                    {
                        if (component.Attributes == null)
                        {
                            RhinoApp.WriteLine("Component attributes are null, creating new attributes");
                            component.CreateAttributes();
                        }
                        
                        component.Attributes.Pivot = new System.Drawing.PointF((float)x, (float)y);
                        
                        doc.AddObject(component, false);
                        
                        doc.NewSolution(false);
                        
                        result = new
                        {
                            id = component.InstanceGuid.ToString(),
                            type = component.GetType().Name,
                            name = component.NickName,
                            x = component.Attributes.Pivot.X,
                            y = component.Attributes.Pivot.Y
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to create component");
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in AddComponent: {ex.Message}");
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
        public static object ConnectComponents(ToolRequest tool)
        {
            var fromData = tool.GetParameter<Dictionary<string, object>>("from");
            var toData = tool.GetParameter<Dictionary<string, object>>("to");
            
            if (fromData == null || toData == null)
            {
                throw new ArgumentException("Source and target component information are required");
            }
            
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
                    
                    string fromIdStr = fromData["id"].ToString();
                    string fromParamName = fromData["parameterName"].ToString();
                    
                    string toIdStr = toData["id"].ToString();
                    string toParamName = toData["parameterName"].ToString();
                    
                    Guid fromId, toId;
                    if (!Guid.TryParse(fromIdStr, out fromId) || !Guid.TryParse(toIdStr, out toId))
                    {
                        throw new ArgumentException("Invalid component ID format");
                    }
                    
                    IGH_Component fromComponent = doc.FindComponent(fromId) as IGH_Component;
                    IGH_Component toComponent = doc.FindComponent(toId) as IGH_Component;
                    
                    if (fromComponent == null || toComponent == null)
                    {
                        throw new ArgumentException("Source or target component not found");
                    }
                    
                    IGH_Param fromParam = null;
                    foreach (var param in fromComponent.Params.Output)
                    {
                        if (param.Name.Equals(fromParamName, StringComparison.OrdinalIgnoreCase))
                        {
                            fromParam = param;
                            break;
                        }
                    }
                    
                    IGH_Param toParam = null;
                    foreach (var param in toComponent.Params.Input)
                    {
                        if (param.Name.Equals(toParamName, StringComparison.OrdinalIgnoreCase))
                        {
                            toParam = param;
                            break;
                        }
                    }
                    
                    if (fromParam == null || toParam == null)
                    {
                        throw new ArgumentException("Source or target parameter not found");
                    }
                    
                    toParam.AddSource(fromParam);
                    
                    doc.NewSolution(false);
                    
                    result = new
                    {
                        from = new
                        {
                            id = fromComponent.InstanceGuid.ToString(),
                            name = fromComponent.NickName,
                            parameter = fromParam.Name
                        },
                        to = new
                        {
                            id = toComponent.InstanceGuid.ToString(),
                            name = toComponent.NickName,
                            parameter = toParam.Name
                        }
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
                throw exception;
            }
            
            return result;
        }
        
        /// <summary>
        /// </summary>
        public static object SetComponentValue(ToolRequest tool)
        {
            string idStr = tool.GetParameter<string>("id");
            string value = tool.GetParameter<string>("value");
            
            if (string.IsNullOrEmpty(idStr))
            {
                throw new ArgumentException("Component ID is required");
            }
            
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
                    
                    Guid id;
                    if (!Guid.TryParse(idStr, out id))
                    {
                        throw new ArgumentException("Invalid component ID format");
                    }
                    
                    IGH_DocumentObject component = doc.FindObject(id, true);
                    if (component == null)
                    {
                        throw new ArgumentException($"Component with ID {idStr} not found");
                    }
                    
                    if (component is GH_Panel panel)
                    {
                        panel.UserText = value;
                    }
                    else if (component is GH_NumberSlider slider)
                    {
                        double doubleValue;
                        if (double.TryParse(value, out doubleValue))
                        {
                            slider.SetSliderValue((decimal)doubleValue);
                        }
                        else
                        {
                            throw new ArgumentException("Invalid slider value format");
                        }
                    }
                    else if (component is IGH_Component ghComponent)
                    {
                        if (ghComponent.Params.Input.Count > 0)
                        {
                            var param = ghComponent.Params.Input[0];
                            if (param is Param_String stringParam)
                            {
                                stringParam.PersistentData.Clear();
                                stringParam.PersistentData.Append(new Grasshopper.Kernel.Types.GH_String(value));
                            }
                            else if (param is Param_Number numberParam)
                            {
                                double doubleValue;
                                if (double.TryParse(value, out doubleValue))
                                {
                                    numberParam.PersistentData.Clear();
                                    numberParam.PersistentData.Append(new Grasshopper.Kernel.Types.GH_Number(doubleValue));
                                }
                                else
                                {
                                    throw new ArgumentException("Invalid number value format");
                                }
                            }
                            else
                            {
                                throw new ArgumentException($"Cannot set value for parameter type {param.GetType().Name}");
                            }
                        }
                        else
                        {
                            throw new ArgumentException("Component has no input parameters");
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"Cannot set value for component type {component.GetType().Name}");
                    }
                    
                    doc.NewSolution(false);
                    
                    result = new
                    {
                        id = component.InstanceGuid.ToString(),
                        type = component.GetType().Name,
                        value = value
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in SetComponentValue: {ex.Message}");
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
        public static object GetComponentInfo(ToolRequest tool)
        {
            string idStr = tool.GetParameter<string>("id");
            
            if (string.IsNullOrEmpty(idStr))
            {
                throw new ArgumentException("Component ID is required");
            }
            
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
                    
                    Guid id;
                    if (!Guid.TryParse(idStr, out id))
                    {
                        throw new ArgumentException("Invalid component ID format");
                    }
                    
                    IGH_DocumentObject component = doc.FindObject(id, true);
                    if (component == null)
                    {
                        throw new ArgumentException($"Component with ID {idStr} not found");
                    }
                    
                    var componentInfo = new Dictionary<string, object>
                    {
                        { "id", component.InstanceGuid.ToString() },
                        { "type", component.GetType().Name },
                        { "name", component.NickName },
                        { "description", component.Description }
                    };
                    
                    if (component is IGH_Component ghComponent)
                    {
                        var inputs = new List<Dictionary<string, object>>();
                        foreach (var param in ghComponent.Params.Input)
                        {
                            inputs.Add(new Dictionary<string, object>
                            {
                                { "name", param.Name },
                                { "nickname", param.NickName },
                                { "description", param.Description },
                                { "type", param.GetType().Name },
                                { "dataType", param.TypeName }
                            });
                        }
                        componentInfo["inputs"] = inputs;
                        
                        var outputs = new List<Dictionary<string, object>>();
                        foreach (var param in ghComponent.Params.Output)
                        {
                            outputs.Add(new Dictionary<string, object>
                            {
                                { "name", param.Name },
                                { "nickname", param.NickName },
                                { "description", param.Description },
                                { "type", param.GetType().Name },
                                { "dataType", param.TypeName }
                            });
                        }
                        componentInfo["outputs"] = outputs;
                    }
                    
                    if (component is GH_Panel panel)
                    {
                        componentInfo["value"] = panel.UserText;
                    }
                    
                    if (component is GH_NumberSlider slider)
                    {
                        componentInfo["value"] = (double)slider.CurrentValue;
                        componentInfo["minimum"] = (double)slider.Slider.Minimum;
                        componentInfo["maximum"] = (double)slider.Slider.Maximum;
                    }
                    
                    result = componentInfo;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in GetComponentInfo: {ex.Message}");
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
        
        private static IGH_DocumentObject CreateComponentByName(string name)
        {
            var obj = Grasshopper.Instances.ComponentServer.ObjectProxies
                .FirstOrDefault(p => p.Desc.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                
            if (obj != null)
            {
                return obj.CreateInstance();
            }
            else
            {
                throw new ArgumentException($"Component with name {name} not found");
            }
        }
    }
}

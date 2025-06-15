using System;
using System.Collections.Generic;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Newtonsoft.Json.Linq;
using System.Linq;
using Rhino;

namespace GrasshopperMCP.Tools
{
    /// <summary>
    /// </summary>
    public static class GeometryToolHandler
    {
        /// <summary>
        /// </summary>
        public static object CreatePoint(ToolRequest tool)
        {
            double x = tool.GetParameter<double>("x");
            double y = tool.GetParameter<double>("y");
            double z = tool.GetParameter<double>("z");
            
            Point3d point = new Point3d(x, y, z);
            
            return new
            {
                id = Guid.NewGuid().ToString(),
                x = point.X,
                y = point.Y,
                z = point.Z
            };
        }
        
        /// <summary>
        /// </summary>
        public static object CreateCurve(ToolRequest tool)
        {
            var pointsData = tool.GetParameter<JArray>("points");
            
            if (pointsData == null || pointsData.Count < 2)
            {
                throw new ArgumentException("At least 2 points are required to create a curve");
            }
            
            List<Point3d> points = new List<Point3d>();
            foreach (var pointData in pointsData)
            {
                double x = pointData["x"].Value<double>();
                double y = pointData["y"].Value<double>();
                double z = pointData["z"]?.Value<double>() ?? 0.0;
                
                points.Add(new Point3d(x, y, z));
            }
            
            Curve curve;
            if (points.Count == 2)
            {
                curve = new LineCurve(points[0], points[1]);
            }
            else
            {
                curve = Curve.CreateInterpolatedCurve(points, 3);
            }
            
            return new
            {
                id = Guid.NewGuid().ToString(),
                pointCount = points.Count,
                length = curve.GetLength()
            };
        }
        
        /// <summary>
        /// </summary>
        public static object CreateCircle(ToolRequest tool)
        {
            var centerData = tool.GetParameter<JObject>("center");
            double radius = tool.GetParameter<double>("radius");
            
            if (centerData == null)
            {
                throw new ArgumentException("Center point is required");
            }
            
            if (radius <= 0)
            {
                throw new ArgumentException("Radius must be greater than 0");
            }
            
            double x = centerData["x"].Value<double>();
            double y = centerData["y"].Value<double>();
            double z = centerData["z"]?.Value<double>() ?? 0.0;
            
            Point3d center = new Point3d(x, y, z);
            
            Circle circle = new Circle(center, radius);
            
            return new
            {
                id = Guid.NewGuid().ToString(),
                center = new { x = center.X, y = center.Y, z = center.Z },
                radius = circle.Radius,
                circumference = circle.Circumference
            };
        }
    }
}

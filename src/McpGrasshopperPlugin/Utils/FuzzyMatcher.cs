using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GH_MCP.Utils
{
    /// <summary>
    /// Utility helpers for fuzzy matching component and parameter names.
    /// </summary>
    public static class FuzzyMatcher
    {
        // Map simplified names to actual Grasshopper component names
        private static readonly Dictionary<string, string> ComponentNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Plane components
            { "plane", "XY Plane" },
            { "xyplane", "XY Plane" },
            { "xy", "XY Plane" },
            { "xzplane", "XZ Plane" },
            { "xz", "XZ Plane" },
            { "yzplane", "YZ Plane" },
            { "yz", "YZ Plane" },
            { "plane3pt", "Plane 3Pt" },
            { "3ptplane", "Plane 3Pt" },
            
            // Basic geometry components
            { "box", "Box" },
            { "cube", "Box" },
            { "rectangle", "Rectangle" },
            { "rect", "Rectangle" },
            { "circle", "Circle" },
            { "circ", "Circle" },
            { "sphere", "Sphere" },
            { "cylinder", "Cylinder" },
            { "cyl", "Cylinder" },
            { "cone", "Cone" },
            
            // Parameter components
            { "slider", "Number Slider" },
            { "numberslider", "Number Slider" },
            { "panel", "Panel" },
            { "point", "Point" },
            { "pt", "Point" },
            { "line", "Line" },
            { "ln", "Line" },
            { "curve", "Curve" },
            { "crv", "Curve" }
        };
        
        // Map simplified parameter names to Grasshopper parameters
        private static readonly Dictionary<string, string> ParameterNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Plane parameters
            { "plane", "Plane" },
            { "base", "Base" },
            { "origin", "Origin" },
            
            // Dimension parameters
            { "radius", "Radius" },
            { "r", "Radius" },
            { "size", "Size" },
            { "xsize", "X Size" },
            { "ysize", "Y Size" },
            { "zsize", "Z Size" },
            { "width", "X Size" },
            { "length", "Y Size" },
            { "height", "Z Size" },
            { "x", "X" },
            { "y", "Y" },
            { "z", "Z" },
            
            // Point parameters
            { "point", "Point" },
            { "pt", "Point" },
            { "center", "Center" },
            { "start", "Start" },
            { "end", "End" },
            
            // Numeric parameters
            { "number", "Number" },
            { "num", "Number" },
            { "value", "Value" },
            
            // Output parameters
            { "result", "Result" },
            { "output", "Output" },
            { "geometry", "Geometry" },
            { "geo", "Geometry" },
            { "brep", "Brep" }
        };
        
        /// <summary>
        /// Gets the closest matching component name.
        /// </summary>
        /// <param name="input">Input component name</param>
        /// <returns>The mapped component name</returns>
        public static string GetClosestComponentName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;
                
            // Try to map directly
            string normalizedInput = input.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            if (ComponentNameMap.TryGetValue(normalizedInput, out string mappedName))
                return mappedName;
                
            // If no mapping was found return the original input
            return input;
        }
        
        /// <summary>
        /// Gets the closest matching parameter name.
        /// </summary>
        /// <param name="input">Input parameter name</param>
        /// <returns>The mapped parameter name</returns>
        public static string GetClosestParameterName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;
                
            // Try to map directly
            string normalizedInput = input.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            if (ParameterNameMap.TryGetValue(normalizedInput, out string mappedName))
                return mappedName;
                
            // If no mapping was found return the original input
            return input;
        }
        
        /// <summary>
        /// Finds the closest string from a list of candidates.
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="candidates">Candidate list</param>
        /// <returns>The closest string</returns>
        public static string FindClosestMatch(string input, IEnumerable<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(input) || candidates == null || !candidates.Any())
                return input;
                
            // First try exact match
            var exactMatch = candidates.FirstOrDefault(c => string.Equals(c, input, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;
                
            // Then check if any candidate contains the input
            var containsMatches = candidates.Where(c => c.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (containsMatches.Count == 1)
                return containsMatches[0];
                
            // Then check prefix match
            var prefixMatches = candidates.Where(c => c.StartsWith(input, StringComparison.OrdinalIgnoreCase)).ToList();
            if (prefixMatches.Count == 1)
                return prefixMatches[0];
                
            // If multiple matches exist, return the shortest one
            if (containsMatches.Any())
                return containsMatches.OrderBy(c => c.Length).First();
                
            // If nothing matched, return the original input
            return input;
        }
    }
}

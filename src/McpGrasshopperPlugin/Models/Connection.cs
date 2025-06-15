using System;

namespace GH_MCP.Models
{
    /// <summary>
    /// Represents a connection endpoint on a component.
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Component GUID
        /// </summary>
        public string ComponentId { get; set; }

        /// <summary>
        /// Parameter name (input or output)
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// Parameter index if name is not specified
        /// </summary>
        public int? ParameterIndex { get; set; }

        /// <summary>
        /// Checks whether the connection information is valid
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(ComponentId) && 
                   (!string.IsNullOrEmpty(ParameterName) || ParameterIndex.HasValue);
        }
    }

    /// <summary>
    /// Represents a pairing between two components.
    /// </summary>
    public class ConnectionPairing
    {
        /// <summary>
        /// Source connection (output)
        /// </summary>
        public Connection Source { get; set; }

        /// <summary>
        /// Target connection (input)
        /// </summary>
        public Connection Target { get; set; }

        /// <summary>
        /// Checks whether the pairing is valid
        /// </summary>
        public bool IsValid()
        {
            return Source != null && Target != null && Source.IsValid() && Target.IsValid();
        }
    }
}

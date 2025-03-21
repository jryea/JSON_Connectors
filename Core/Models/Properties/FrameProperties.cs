using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents properties for frame elements (beams, columns, braces) in the structural model
    /// </summary>
    public class FrameProperties
    {
        /// <summary>
        /// Name of the frame property
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// ID of the material for this frame element
        /// </summary>
        public string MaterialId { get; set; }

        /// <summary>
        /// Shape of the frame section (e.g., "W", "HSS", "Pipe")
        /// </summary>
        public string Shape { get; set; }

        /// <summary>
        /// Dimensions of the frame section
        /// </summary>
        public Dictionary<string, double> Dimensions { get; set; } = new Dictionary<string, double>
        {
            { "depth", 0 },
            { "width", 0 }
        };

        /// <summary>
        /// Modifiers for section properties (if applicable)
        /// </summary>
        public Dictionary<string, object> Modifiers { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Reinforcement details (if applicable)
        /// </summary>
        public Dictionary<string, object> Rebar { get; set; } = new Dictionary<string, object>();
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents properties for wall elements in the structural model
    /// </summary>
    public class WallProperties
    {
        /// <summary>
        /// Name of the wall property
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Material of the wall
        /// </summary>
        public Material Material { get; set; }

        /// <summary>
        /// Thickness of the wall in model units (typically inches)
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// Additional wall-specific properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }
}
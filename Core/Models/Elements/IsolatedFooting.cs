using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents an isolated footing element in the structural model
    /// </summary>
    public class IsolatedFooting
    {
        /// <summary>
        /// Location point of the isolated footing in 3D space
        /// </summary>
        public Point3D Point { get; set; }

        /// <summary>
        /// Optional properties ID for the footing
        /// </summary>
        public string PropertiesId { get; set; }

        /// <summary>
        /// Optional dimensions for the footing
        /// </summary>
        public Dictionary<string, double> Dimensions { get; set; } = new Dictionary<string, double>();
    }
}
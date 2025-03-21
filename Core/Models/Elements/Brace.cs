using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents a brace element in the structural model
    /// </summary>
    public class Brace
    {
        /// <summary>
        /// ID of the material for this brace
        /// </summary>
        public string MaterialId { get; set; }

        /// <summary>
        /// Section ID information with analysis properties
        /// </summary>
        public Dictionary<string, string> SectionId { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// ID of the level this brace belongs to
        /// </summary>
        public string LevelId { get; set; }

        /// <summary>
        /// Starting point of the brace in 3D space
        /// </summary>
        public Point3D StartPoint { get; set; }

        /// <summary>
        /// Ending point of the brace in 3D space
        /// </summary>
        public Point3D EndPoint { get; set; }
    }
}
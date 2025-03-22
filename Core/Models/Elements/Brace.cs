using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Properties;
using Core.Models.ModelLayout;
using Core.Models.Loads;
using Core.Models.Metadata;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents a brace element in the structural model
    /// </summary>
    public class Brace
    {
        public Level BaseLevel { get; set; }

        public Level TopLevel { get; set; } 

        /// <summary>
        /// Starting point of the brace in 3D space
        /// </summary>
        public Point3D StartPoint { get; set; }

        /// <summary>
        /// Ending point of the brace in 3D space
        /// </summary>
        public Point3D EndPoint { get; set; }

        public FrameProperties FrameProperties { get; set; }
    }
}
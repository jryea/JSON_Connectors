using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents a beam element in the structural model
    /// </summary>
    public class Beam
    {
        /// <summary>
        /// Starting point of the beam in 2D plan view
        /// </summary>
        public Point2D StartPoint { get; set; }

        /// <summary>
        /// Ending point of the beam in 2D plan view
        /// </summary>
        public Point2D EndPoint { get; set; }

        /// <summary>
        /// ID of the level this beam belongs to
        /// </summary>
        public string LevelId { get; set; }

        /// <summary>
        /// ID of the properties for this beam
        /// </summary>
        public string PropertiesId { get; set; }

        /// <summary>
        /// Indicates if this beam is part of the lateral system
        /// </summary>
        public bool IsLateral { get; set; }

        /// <summary>
        /// Indicates if this beam is a joist
        /// </summary>
        public bool IsJoist { get; set; }
    }
}
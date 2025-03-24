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
    /// Represents a wall element in the structural model
    /// </summary>
    public class Wall
    {
        /// <summary>
        /// Collection of 2D points defining the wall geometry in plan view
        /// </summary>
        public List<Point2D> Points { get; set; } = new List<Point2D>();

        /// <summary>
        /// ID of the properties for this wall
        /// </summary>
        public string PropertiesId { get; set; }

        /// <summary>
        /// ID of the pier/spandrel configuration for this wall
        /// </summary>
        public string PierSpandrelId { get; set; }
    }
}
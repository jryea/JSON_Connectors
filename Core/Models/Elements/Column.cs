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
    /// Represents a column element in the structural model
    /// </summary>
    public class Column
    {
        /// <summary>
        /// Starting point of the column in 3D space
        /// </summary>
        public Point2D StartPoint { get; set; }

        /// <summary>
        /// Ending point of the column in 3D space
        /// </summary>
        public Point2D EndPoint { get; set; }

        /// <summary>
        /// ID of the base level for this column
        /// </summary>
        public Level BaseLevel { get; set; }

        /// <summary>
        /// ID of the top level for this column
        /// </summary>
        public Level TopLevel { get; set; }

        /// <summary>
        /// ID of the section properties for this column
        /// </summary>
        public FrameProperties FrameProperties { get; set; }
  
    }
}
using System;
using System.Collections.Generic;
using System.Text;

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
        public Point3D StartPoint { get; set; }

        /// <summary>
        /// Ending point of the column in 3D space
        /// </summary>
        public Point3D EndPoint { get; set; }

        /// <summary>
        /// ID of the base level for this column
        /// </summary>
        public string BaseLevelId { get; set; }

        /// <summary>
        /// ID of the top level for this column
        /// </summary>
        public string TopLevelId { get; set; }

        /// <summary>
        /// ID of the section properties for this column
        /// </summary>
        public string SectionId { get; set; }

        /// <summary>
        /// Analysis properties for this column
        /// </summary>
        public Dictionary<string, string> Analysis { get; set; } = new Dictionary<string, string>();
    }
}
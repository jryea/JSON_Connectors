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
    /// Represents a floor element in the structural model
    /// </summary>
    public class Floor
    {
        /// <summary>
        /// ID of the level this floor belongs to
        /// </summary>
        public Level Level { get; set; }

        /// <summary>
        /// ID of the properties for this floor
        /// </summary>
        public FloorProperties FloorProperties { get; set; }

        /// <summary>
        /// Collection of 3D points defining the floor geometry
        /// </summary>
        public List<Point2D> Points { get; set; } = new List<Point2D>();

        /// <summary>
        /// ID of the diaphragm assigned to this floor
        /// </summary>
        public Diaphragm Diaphragm { get; set; }

        /// <summary>
        /// ID of the surface load assigned to this floor
        /// </summary>
        public SurfaceLoad SurfaceLoad { get; set; }
    }
}
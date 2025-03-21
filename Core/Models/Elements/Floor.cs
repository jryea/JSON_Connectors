using System;
using System.Collections.Generic;
using System.Text;

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
        public string LevelId { get; set; }

        /// <summary>
        /// ID of the properties for this floor
        /// </summary>
        public string PropertiesId { get; set; }

        /// <summary>
        /// Collection of 3D points defining the floor geometry
        /// </summary>
        public List<Point3D> Points { get; set; } = new List<Point3D>();

        /// <summary>
        /// ID of the diaphragm assigned to this floor
        /// </summary>
        public string DiaphragmId { get; set; }

        /// <summary>
        /// ID of the surface load assigned to this floor
        /// </summary>
        public string SurfaceLoadId { get; set; }
    }
}
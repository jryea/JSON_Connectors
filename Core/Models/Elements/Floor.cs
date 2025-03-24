using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents a floor element in the structural model
    /// </summary>
    public class Floor : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the floor
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// ID of the level this floor belongs to
        /// </summary>
        public string LevelId { get; set; }

        /// <summary>
        /// ID of the properties for this floor
        /// </summary>
        public string PropertiesId { get; set; }

        /// <summary>
        /// Collection of points defining the floor geometry
        /// </summary>
        public List<Point2D> Points { get; set; } = new List<Point2D>();

        /// <summary>
        /// ID of the diaphragm for this floor
        /// </summary>
        public string DiaphragmId { get; set; }

        /// <summary>
        /// ID of the surface load for this floor
        /// </summary>
        public string SurfaceLoadId { get; set; }

        /// <summary>
        /// Creates a new Floor with a generated ID
        /// </summary>
        public Floor()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR);
            Points = new List<Point2D>();
        }

        /// <summary>
        /// Creates a new Floor with specified properties
        /// </summary>
        /// <param name="levelId">Level ID</param>
        /// <param name="propertiesId">Properties ID</param>
        /// <param name="points">Points defining the floor geometry</param>
        /// <param name="diaphragmId">Diaphragm ID</param>
        /// <param name="surfaceLoadId">Surface load ID</param>
        public Floor(string levelId, string propertiesId, List<Point2D> points, string diaphragmId = null, string surfaceLoadId = null) : this()
        {
            LevelId = levelId;
            PropertiesId = propertiesId;
            Points = points ?? new List<Point2D>();
            DiaphragmId = diaphragmId;
            SurfaceLoadId = surfaceLoadId;
        }
    }
}
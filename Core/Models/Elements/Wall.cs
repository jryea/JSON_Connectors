using System.Collections.Generic;
using Core.Models.Geometry;
using Core.Utilities;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents a wall element in the structural model
    /// </summary>
    public class Wall : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the wall
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Collection of 2D points defining the wall geometry in plan view
        /// </summary>
        public List<Point2D> Points { get; set; } = new List<Point2D>();

        /// <summary>
        /// ID of the base level for this wall
        /// </summary>
        public string BaseLevelId { get; set; }

        /// <summary>
        /// ID of the top level for this wall
        /// </summary>
        public string TopLevelId { get; set; }

        /// <summary>
        /// ID of the properties for this wall
        /// </summary>
        public string PropertiesId { get; set; }

        /// <summary>
        /// ID of the pier/spandrel configuration for this wall
        /// </summary>
        public string PierSpandrelId { get; set; }

        /// <summary>
        /// Creates a new Wall with a generated ID
        /// </summary>
        public Wall()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.WALL);
            Points = new List<Point2D>();
        }

        /// <summary>
        /// Creates a new Wall with the specified properties
        /// </summary>
        /// <param name="points">Points defining the wall geometry</param>
        /// <param name="propertiesId">Properties ID</param>
        /// <param name="pierSpandrelId">Pier/spandrel configuration ID</param>
        public Wall(List<Point2D> points, string propertiesId, string pierSpandrelId = null) : this()
        {
            Points = points ?? new List<Point2D>();
            PropertiesId = propertiesId;
            PierSpandrelId = pierSpandrelId;
        }
    }
}
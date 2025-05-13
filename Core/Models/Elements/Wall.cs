using System.Collections.Generic;
using Core.Models.Geometry;
using Core.Utilities;

namespace Core.Models.Elements
{
    // Represents a wall element in the structural model
    public class Wall : IIdentifiable
    {
        // Unique identifier for the wall
        public string Id { get; set; }

        // Collection of 2D points defining the wall geometry in plan view
        public List<Point2D> Points { get; set; } = new List<Point2D>();

        // ID of the base level for this wall
        public string BaseLevelId { get; set; }

        // ID of the top level for this wall
        public string TopLevelId { get; set; }

        // ID of the properties for this wall
        public string PropertiesId { get; set; }

        // ID of the pier/spandrel configuration for this wall
        public string PierId { get; set; }
        public string SpandrelId { get; set; }

        public bool IsLateral { get; set; } = false;    

        // Creates a new Wall with a generated ID
        public Wall()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.WALL);
            Points = new List<Point2D>();
        }

        // Creates a new Wall with the specified properties
        public Wall(List<Point2D> points, string propertiesId) : this()
        {
            Points = points ?? new List<Point2D>();
            PropertiesId = propertiesId;
        }
    }
}
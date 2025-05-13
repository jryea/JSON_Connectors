using System.Collections.Generic;
using Core.Models.Geometry;
using Core.Utilities;
using static Core.Models.SoftwareSpecific.ETABSModifiers;

namespace Core.Models.Elements
{
    // Represents a floor element in the structural model
    public class Floor : IIdentifiable
    {
        // Unique identifier for the floor
        public string Id { get; set; }

        // ID of the level this floor belongs to
        public string LevelId { get; set; }

        // ID of the properties for this floor
        public string FloorPropertiesId { get; set; }

        // Collection of points defining the floor geometry
        public List<Point2D> Points { get; set; } = new List<Point2D>();

        // ID of the diaphragm for this floor
        public string DiaphragmId { get; set; }

        // ID of the surface load for this floor
        public string SurfaceLoadId { get; set; }

        // ETABS-specific properties
        public ETABSShellModifiers ETABSModifiers { get; set; } = new ETABSShellModifiers();

        // Creates a new Floor with a generated ID
        public Floor()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR);
            Points = new List<Point2D>();
        }

        public Floor(string levelId, string propertiesId, List<Point2D> points, string diaphragmId = null, string surfaceLoadId = null) : this()
        {
            LevelId = levelId;
            FloorPropertiesId = propertiesId;
            Points = points ?? new List<Point2D>();
            DiaphragmId = diaphragmId;
            SurfaceLoadId = surfaceLoadId;
        }
    }
}
using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models.Properties
{
    // Represents properties for wall elements in the structural model
    public class WallProperties : IIdentifiable
    {
        // Unique identifier for the wall properties
        public string Id { get; set; }

        // Name of the wall properties
        public string Name { get; set; }

        // ID of the material for this wall
        public string MaterialId { get; set; }

        // Thickness of the wall in model units
        public double Thickness { get; set; }

        // Additional wall-specific properties
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        // Creates a new WallProperties with a generated ID
        public WallProperties()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.WALL_PROPERTIES);
            Properties = new Dictionary<string, object>();
        }

        // Creates a new WallProperties with specified properties

        public WallProperties(string name, string materialId, double thickness) : this()
        {
            Name = name;
            MaterialId = materialId;
            Thickness = thickness;
        }
    }
}
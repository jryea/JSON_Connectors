using System.Collections.Generic;
using Core.Models.SoftwareSpecific;
using Core.Utilities;
using static Core.Models.SoftwareSpecific.ETABSModifiers;
using static Core.Models.SoftwareSpecific.RAMReinforcement;
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

        public double UnitWeightForSelfWeight { get; set; } = 150;

        public RAMReinforcement Reinforcement { get; set; } = new RAMReinforcement();

        // ETABS-specific properties
        public ETABSShellModifiers ETABSModifiers { get; set; } = new ETABSShellModifiers();

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

    public class Reinforcement
    {
        // This is the Fy value for the main wall panel reinforcement.
        public double FyDistributed { get; set; } = 60; // Default 60 ksi

        // This is the Fy value for steel in wall boundary elements or end zones
        public double FuDistributed { get; set; } = 60; // Default 60 ksi

        // This is the Fy value for transverse reinforcement including 
        // confinement ties, stirrups, and cross-links in the wall
        public double FyTiesLinks { get; set; } = 60;   // Default 60 ksi
    }
}
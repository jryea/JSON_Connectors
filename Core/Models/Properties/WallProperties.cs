using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents properties for wall elements in the structural model
    /// </summary>
    public class WallProperties : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the wall properties
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the wall properties
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// ID of the material for this wall
        /// </summary>
        public string MaterialId { get; set; }

        /// <summary>
        /// Thickness of the wall in model units
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// Additional wall-specific properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new WallProperties with a generated ID
        /// </summary>
        public WallProperties()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.WALL_PROPERTIES);
            Properties = new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a new WallProperties with specified properties
        /// </summary>
        /// <param name="name">Name of the wall properties</param>
        /// <param name="materialId">ID of the material for this wall</param>
        /// <param name="thickness">Thickness of the wall in model units</param>
        public WallProperties(string name, string materialId, double thickness) : this()
        {
            Name = name;
            MaterialId = materialId;
            Thickness = thickness;
        }
    }
}
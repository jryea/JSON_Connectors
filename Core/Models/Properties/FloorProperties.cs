using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents properties for floor elements in the structural model
    /// </summary>
    public class FloorProperties : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the floor properties
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the floor properties
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Type of floor (e.g., "Slab", "Composite", "NonComposite")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Thickness of the floor in model units
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// ID of the material for this floor
        /// </summary>
        public string MaterialId { get; set; }

        /// <summary>
        /// Reinforcement information
        /// </summary>
        public string Reinforcement { get; set; }

        /// <summary>
        /// Additional slab-specific properties
        /// </summary>
        public Dictionary<string, object> SlabProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Additional deck-specific properties
        /// </summary>
        public Dictionary<string, object> DeckProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new FloorProperties with a generated ID
        /// </summary>
        public FloorProperties()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES);
            SlabProperties = new Dictionary<string, object>();
            DeckProperties = new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a new FloorProperties with specified properties
        /// </summary>
        /// <param name="name">Name of the floor properties</param>
        /// <param name="type">Type of floor</param>
        /// <param name="thickness">Thickness of the floor in model units</param>
        /// <param name="materialId">ID of the material for this floor</param>
        public FloorProperties(string name, string type, double thickness, string materialId) : this()
        {
            Name = name;
            Type = type;
            Thickness = thickness;
            MaterialId = materialId;

            // Initialize default properties based on type
            InitializeDefaultProperties();
        }

        /// <summary>
        /// Initializes default properties based on the floor type
        /// </summary>
        private void InitializeDefaultProperties()
        {
            switch (Type?.ToLower())
            {
                case "slab":
                    SlabProperties["isRibbed"] = false;
                    SlabProperties["isWaffle"] = false;
                    SlabProperties["isTwoWay"] = true;
                    break;

                case "composite":
                    DeckProperties["deckType"] = "Composite";
                    DeckProperties["deckDepth"] = 1.5; // inches
                    DeckProperties["deckGage"] = 22;
                    DeckProperties["toppingThickness"] = Thickness - 1.5;
                    break;

                case "noncomposite":
                    DeckProperties["deckType"] = "MetalDeck";
                    DeckProperties["deckDepth"] = Thickness;
                    DeckProperties["deckGage"] = 22;
                    break;
            }
        }
    }
}
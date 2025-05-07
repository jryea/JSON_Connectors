using Core.Models.Properties.Floors;
using Core.Utilities;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents properties for floor elements in the structural model with type-specific properties
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
        /// ID of the design code applicable to this floor
        /// </summary>
        public string DesignCodeId { get; set; }

        /// <summary>
        /// Reinforcement information
        /// </summary>
        public string Reinforcement { get; set; }

        /// <summary>
        /// Slab-specific properties (non-null when Type is "Slab")
        /// </summary>
        public SlabProperties SlabProps { get; set; }

        /// <summary>
        /// Deck-specific properties (non-null when Type is "Composite" or "NonComposite")
        /// </summary>
        public DeckProperties DeckProps { get; set; }

        /// <summary>
        /// Creates a new FloorProperties with a generated ID
        /// </summary>
        public FloorProperties()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES);
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

            // Initialize type-specific properties
            InitializeProperties();
        }

        /// <summary>
        /// Initializes type-specific properties based on the floor type
        /// </summary>
        public void InitializeProperties()
        {
            // Reset all type-specific properties
            SlabProps = null;
            DeckProps = null;

            // Initialize properties based on floor type
            switch (Type?.ToLower())
            {
                case "slab":
                    SlabProps = new SlabProperties
                    {
                        IsRibbed = false,
                        IsWaffle = false,
                        IsTwoWay = true
                    };
                    break;

                case "composite":
                    DeckProps = new DeckProperties
                    {
                        DeckType = "Composite",
                        DeckDepth = 1.5, // inches
                        DeckGage = 22,
                        ToppingThickness = Thickness - 1.5
                    };
                    break;

                case "noncomposite":
                    DeckProps = new DeckProperties
                    {
                        DeckType = "NonComposite",
                        DeckDepth = Thickness,
                        DeckGage = 22
                    };
                    break;
            }
        }
    }
}
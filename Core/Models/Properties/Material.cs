using Core.Models.Properties.Materials;
using Core.Utilities;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents a material in the structural model with type-specific properties
    /// </summary>
    public class Material : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the material
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the material
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Type of material (e.g., "Concrete", "Steel", "Wood", "Masonry", "ColdForm")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// ID of the design code applicable to this material
        /// </summary>
        public string DesignCodeId { get; set; }

        /// <summary>
        /// Concrete-specific properties (non-null when Type is "Concrete")
        /// </summary>
        public ConcreteProperties ConcreteProps { get; set; }

        /// <summary>
        /// Steel-specific properties (non-null when Type is "Steel")
        /// </summary>
        public SteelProperties SteelProps { get; set; }

        /// <summary>
        /// Wood-specific properties (non-null when Type is "Wood")
        /// </summary>
        public WoodProperties WoodProps { get; set; }

        /// <summary>
        /// Masonry-specific properties (non-null when Type is "Masonry")
        /// </summary>
        public MasonryProperties MasonryProps { get; set; }

        /// <summary>
        /// Cold-formed steel specific properties (non-null when Type is "ColdForm")
        /// </summary>
        public ColdFormProperties ColdFormProps { get; set; }

        /// <summary>
        /// Creates a new Material with a generated ID
        /// </summary>
        public Material()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL);
        }

        /// <summary>
        /// Creates a new Material with the specified properties
        /// </summary>
        /// <param name="name">Name of the material</param>
        /// <param name="type">Type of material</param>
        public Material(string name, string type) : this()
        {
            Name = name;
            Type = type;

            // Initialize type-specific properties
            InitializeProperties();
        }

        /// <summary>
        /// Initialize type-specific properties based on material type
        /// </summary>
        public void InitializeProperties()
        {
            // Reset all type-specific properties
            ConcreteProps = null;
            SteelProps = null;
            WoodProps = null;
            MasonryProps = null;
            ColdFormProps = null;

            // Initialize properties based on material type
            switch (Type?.ToLower())
            {
                case "concrete":
                    ConcreteProps = new ConcreteProperties();
                    break;

                case "steel":
                    SteelProps = new SteelProperties();
                    break;

                case "wood":
                    WoodProps = new WoodProperties();
                    break;

                case "masonry":
                    MasonryProps = new MasonryProperties();
                    break;

                case "coldform":
                    ColdFormProps = new ColdFormProperties();
                    break;
            }
        }
    }
}
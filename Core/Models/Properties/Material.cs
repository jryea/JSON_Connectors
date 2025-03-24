using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents a material in the structural model
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
        /// Type of material (e.g., "Concrete", "Steel", "Wood")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Reinforcing type (if applicable)
        /// </summary>
        public string Reinforcing { get; set; }

        /// <summary>
        /// Directional symmetry type
        /// </summary>
        public string DirectionalSymmetryType { get; set; }

        /// <summary>
        /// Design data specific to the material
        /// </summary>
        public Dictionary<string, object> DesignData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new Material with a generated ID
        /// </summary>
        public Material()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL);
            DesignData = new Dictionary<string, object>();
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

            // Add default properties based on material type
            InitializeDefaultProperties();
        }

        /// <summary>
        /// Initialize default material properties based on type
        /// </summary>
        private void InitializeDefaultProperties()
        {
            switch (Type?.ToLower())
            {
                case "concrete":
                    DesignData["fc"] = 4000.0; // psi
                    DesignData["weightDensity"] = 150.0; // pcf
                    DesignData["elasticModulus"] = 3600000.0; // psi
                    DesignData["poissonsRatio"] = 0.2;
                    break;

                case "steel":
                    DesignData["fy"] = 50000.0; // psi
                    DesignData["fu"] = 65000.0; // psi
                    DesignData["elasticModulus"] = 29000000.0; // psi
                    DesignData["weightDensity"] = 490.0; // pcf
                    DesignData["poissonsRatio"] = 0.3;
                    break;

                case "wood":
                    DesignData["fb"] = 1000.0; // psi
                    DesignData["elasticModulus"] = 1600000.0; // psi
                    DesignData["weightDensity"] = 35.0; // pcf
                    DesignData["poissonsRatio"] = 0.2;
                    break;

                case "masonry":
                    DesignData["fm"] = 1500.0; // psi
                    DesignData["elasticModulus"] = 1350000.0; // psi
                    DesignData["weightDensity"] = 125.0; // pcf
                    DesignData["poissonsRatio"] = 0.2;
                    break;
            }
        }
    }
}
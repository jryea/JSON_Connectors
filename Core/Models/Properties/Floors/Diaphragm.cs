using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models.Properties.Floors
{
    /// <summary>
    /// Represents a diaphragm property in the structural model
    /// </summary>
    public class Diaphragm : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the diaphragm
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the diaphragm
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Type of diaphragm (e.g., "Rigid", "Semi-Rigid", "Flexible")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Stiffness modification factor
        /// </summary>
        public double StiffnessFactor { get; set; }

        /// <summary>
        /// Mass modification factor
        /// </summary>
        public double MassFactor { get; set; }

        /// <summary>
        /// Additional diaphragm-specific properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new Diaphragm with a generated ID
        /// </summary>
        public Diaphragm()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.DIAPHRAGM);
            Properties = new Dictionary<string, object>();

            // Set default values
            Type = "Rigid";
            StiffnessFactor = 1.0;
            MassFactor = 1.0;
        }

        /// <summary>
        /// Creates a new Diaphragm with specified properties
        /// </summary>
        /// <param name="name">Name of the diaphragm</param>
        /// <param name="type">Type of diaphragm</param>
        public Diaphragm(string name, string type) : this()
        {
            Name = name;
            Type = type;

            // Initialize properties based on type
            InitializeProperties();
        }

        /// <summary>
        /// Initialize properties based on diaphragm type
        /// </summary>
        private void InitializeProperties()
        {
            switch (Type?.ToLower())
            {
                case "rigid":
                    Properties["isRigid"] = true;
                    Properties["isFlexible"] = false;
                    Properties["isSemiRigid"] = false;
                    break;

                case "semi-rigid":
                case "semirigid":
                    Properties["isRigid"] = false;
                    Properties["isFlexible"] = false;
                    Properties["isSemiRigid"] = true;
                    break;

                case "flexible":
                    Properties["isRigid"] = false;
                    Properties["isFlexible"] = true;
                    Properties["isSemiRigid"] = false;
                    break;
            }
        }
    }
}
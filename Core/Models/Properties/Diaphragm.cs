using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models.Properties
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
        /// Additional diaphragm-specific properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new Diaphragm with a generated ID
        /// </summary>
        public Diaphragm()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.DIAPHRAGM);
        }
    }
}
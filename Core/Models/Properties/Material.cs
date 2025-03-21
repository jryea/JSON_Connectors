using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents a material in the structural model
    /// </summary>
    public class Material
    {
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
    }
}
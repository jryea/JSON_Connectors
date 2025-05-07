using System.Collections.Generic;

namespace Core.Models.Properties.Materials
{
    /// <summary>
    /// Base class for all material-specific properties
    /// </summary>
    public class MaterialProperties
    {
        /// <summary>
        /// Weight density of the material
        /// </summary>
        public double WeightDensity { get; set; }

        /// <summary>
        /// Elastic modulus (Young's modulus) of the material
        /// </summary>
        public double ElasticModulus { get; set; }

        /// <summary>
        /// Poisson's ratio of the material
        /// </summary>
        public double PoissonsRatio { get; set; }

        /// <summary>
        /// Additional properties specific to different software applications
        /// </summary>
        public Dictionary<string, object> SoftwareSpecificProperties { get; set; } = new Dictionary<string, object>();
    }
}
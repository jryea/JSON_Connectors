namespace Core.Models.Properties.Materials
{
    /// <summary>
    /// Properties specific to masonry materials
    /// </summary>
    public class MasonryProperties : MaterialProperties
    {
        /// <summary>
        /// Compressive strength of masonry (f'm)
        /// </summary>
        public double Fm { get; set; }

        /// <summary>
        /// Type of masonry unit (clay brick, CMU, etc.)
        /// </summary>
        public string UnitType { get; set; }

        /// <summary>
        /// Type of mortar used
        /// </summary>
        public string MortarType { get; set; }

        /// <summary>
        /// Compressive strength of grout
        /// </summary>
        public double GroutStrength { get; set; }

        /// <summary>
        /// Creates a new MasonryProperties with default values
        /// </summary>
        public MasonryProperties()
        {
            // Set default values for CMU masonry
            Fm = 1500.0; // psi
            ElasticModulus = 1350000.0; // psi (approximated as 900*f'm)
            WeightDensity = 125.0; // pcf
            PoissonsRatio = 0.2;
            UnitType = "CMU";
            MortarType = "Type S";
            GroutStrength = 2000.0; // psi
        }
    }
}
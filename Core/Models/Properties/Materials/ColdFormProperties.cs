namespace Core.Models.Properties.Materials
{
    /// <summary>
    /// Properties specific to cold-formed steel materials
    /// </summary>
    public class ColdFormProperties : MaterialProperties
    {
        /// <summary>
        /// Yield strength (Fy)
        /// </summary>
        public double Fy { get; set; }

        /// <summary>
        /// Ultimate strength (Fu)
        /// </summary>
        public double Fu { get; set; }

        /// <summary>
        /// Grade designation
        /// </summary>
        public string Grade { get; set; }

        /// <summary>
        /// Coating type
        /// </summary>
        public string CoatingType { get; set; }

        /// <summary>
        /// Base metal thickness
        /// </summary>
        public double BaseThickness { get; set; }

        /// <summary>
        /// Creates a new ColdFormProperties with default values
        /// </summary>
        public ColdFormProperties()
        {
            // Set default values for 33 ksi cold-formed steel
            Fy = 33000.0; // psi
            Fu = 45000.0; // psi
            ElasticModulus = 29500000.0; // psi
            WeightDensity = 490.0; // pcf
            PoissonsRatio = 0.3;
            Grade = "33 ksi";
            CoatingType = "G60";
            BaseThickness = 0.0346; // inches (20 gauge)
        }
    }
}
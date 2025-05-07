namespace Core.Models.Properties.Materials
{
    /// <summary>
    /// Properties specific to steel materials
    /// </summary>
    public class SteelProperties : MaterialProperties
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
        /// Grade type (A36, A992, etc.)
        /// </summary>
        public string Grade { get; set; }

        /// <summary>
        /// Creates a new SteelProperties with default values
        /// </summary>
        public SteelProperties()
        {
            // Set default values for A992 steel
            Fy = 50000.0; // psi
            Fu = 65000.0; // psi
            ElasticModulus = 29000000.0; // psi
            WeightDensity = 490.0; // pcf
            PoissonsRatio = 0.3;
            Grade = "A992";
        }
    }
}
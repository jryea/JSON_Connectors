namespace Core.Models.Properties.Materials
{
    /// <summary>
    /// Properties specific to concrete materials
    /// </summary>
    public class ConcreteProperties : MaterialProperties
    {
        /// <summary>
        /// Compressive strength of concrete (f'c)
        /// </summary>
        public double Fc { get; set; }

        /// <summary>
        /// Weight density class (Normal, Lightweight)
        /// </summary>
        public string WeightClass { get; set; }

        /// <summary>
        /// Shear strength reduction factor
        /// </summary>
        public double ShearStrengthReductionFactor { get; set; }

        /// <summary>
        /// Creates a new ConcreteProperties with default values
        /// </summary>
        public ConcreteProperties()
        {
            // Set default values for normal weight concrete
            Fc = 4000.0; // psi
            WeightDensity = 150.0; // pcf
            ElasticModulus = 3600000.0; // psi (simplified as 57000*sqrt(f'c))
            PoissonsRatio = 0.2;
            WeightClass = "Normal";
            ShearStrengthReductionFactor = 0.75;
        }
    }
}
namespace Core.Models.Properties.Materials
{
    /// <summary>
    /// Properties specific to wood materials
    /// </summary>
    public class WoodProperties : MaterialProperties
    {
        /// <summary>
        /// Bending design value (Fb)
        /// </summary>
        public double Fb { get; set; }

        /// <summary>
        /// Tension parallel to grain design value (Ft)
        /// </summary>
        public double Ft { get; set; }

        /// <summary>
        /// Compression parallel to grain design value (Fc)
        /// </summary>
        public double Fc { get; set; }

        /// <summary>
        /// Compression perpendicular to grain design value (Fc⊥)
        /// </summary>
        public double FcPerp { get; set; }

        /// <summary>
        /// Horizontal shear design value (Fv)
        /// </summary>
        public double Fv { get; set; }

        /// <summary>
        /// Species of wood
        /// </summary>
        public string Species { get; set; }

        /// <summary>
        /// Grade of wood
        /// </summary>
        public string Grade { get; set; }

        /// <summary>
        /// Creates a new WoodProperties with default values
        /// </summary>
        public WoodProperties()
        {
            // Set default values for Douglas Fir-Larch, No.1
            Fb = 1000.0; // psi
            Ft = 675.0; // psi
            Fc = 1500.0; // psi
            FcPerp = 625.0; // psi
            Fv = 180.0; // psi
            ElasticModulus = 1600000.0; // psi
            WeightDensity = 35.0; // pcf
            PoissonsRatio = 0.2;
            Species = "Douglas Fir-Larch";
            Grade = "No.1";
        }
    }
}
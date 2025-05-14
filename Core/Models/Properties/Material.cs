using Core.Utilities;
using Core.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using static Core.Models.SoftwareSpecific.ETABSModifiers;   

namespace Core.Models.Properties
{
    public class Material : IIdentifiable
    {
        public string Id { get; set; }
        public string Name { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DirectionalSymmetryType DirectionalSymmetryType { get; set; } = DirectionalSymmetryType.Isotropic;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MaterialType Type { get; set; }

        public double WeightPerUnitVolume { get; set; } // Weight per unit volume (pcf)   
        public double MassPerUnitVolume { get; set; } // Elastic modulus (p-s^2/ft^4)
        public double ElasticModulus { get; set; } // Elastic modulus (psi)
        public double PoissonsRatio { get; set; } // Poisson's ratio    
        public double CoefficientOfThermalExpansion { get; set; } // Coefficient of thermal expansion (1/°F)    
        public double ShearModulus { get; set; } // Shear modulus (psi) 


        public ConcreteProperties ConcreteProps { get; set; }
        public SteelProperties SteelProps { get; set; }

        public Material()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL);
        }

        public Material(string name, MaterialType type) : this()
        {
            Name = name;
            Type = type;
            InitializeProperties();
        }

        // Initialize type-specific properties based on material type
        public void InitializeProperties()
        {
            // Reset all type-specific properties
            ConcreteProps = null;
            SteelProps = null;

            // Initialize properties based on material type
            switch (Type)
            {
                case MaterialType.Concrete:
                    ConcreteProps = new ConcreteProperties();
                    WeightPerUnitVolume = 150; // Default for concrete
                    ElasticModulus = 3604996.5; // Default for concrete
                    PoissonsRatio = 0.2; // Default for concrete
                    CoefficientOfThermalExpansion = 0.0000055; // Default for concrete  
                    break;

                case MaterialType.Steel:
                    SteelProps = new SteelProperties();
                    WeightPerUnitVolume = 490; // Default for steel
                    ElasticModulus = 29000000; // Default for steel
                    PoissonsRatio = 0.3; // Default for steel
                    CoefficientOfThermalExpansion = 0.0000065; // Default for steel
                    break;
            }
        }
    }

    // Properties specific to steel materials
    public class SteelProperties
    {
        private double _fy;

        // Minimum Yield stress (psi)
        public double Fy
        {
            get => _fy;
            set
            {
                _fy = value;
                Grade = $"Grade {_fy / 1000:0.#}";
            }
        }

        // Minimum Tensile Strength (psi)
        public double Fu { get; set; }

        // Expected Yield Stress (psi)
        public double Fye { get; set; }

        // Expected Tensile Strength (psi)
        public double Fue { get; set; }

        // Grade type (A36, A992, etc.)
        public string Grade { get; private set; }

        // Creates a new SteelProperties with default values
        public SteelProperties()
        {
            // Set default values for A992 steel
            Fy = 50000.0; // psi
            Fu = 65000.0; // psi
            Fye = 55000.0; // psi   
            Fue = 71500.0; // psi
        }
    }

    // Properties specific to concrete materials
    public class ConcreteProperties
    {
        // Compressive strength field  
        private double _fc;

        // Compressive strength of concrete, f'c (psi)
        public double Fc
        {
            get => _fc;
            set
            {
                _fc = value;
                UpdateGrade();
            }
        }

        private void UpdateGrade()
        {
            if (WeightClass == WeightClass.Lightweight)
            {
                Grade = $"f'c {_fc} psi (LW)";
            }
            else
            {
                Grade = $"f'c {_fc} psi";
            }
        }

        // Weight density class (Normal, Lightweight)
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WeightClass WeightClass { get; set; }

        // Shear strength reduction factor
        public double ShearStrengthReductionFactor { get; set; }

        public string Grade { get; private set; }

        // Creates a new ConcreteProperties with default values
        public ConcreteProperties()
        {
            // Set default values for normal weight concrete
            WeightClass = WeightClass.Normal;
            Fc = 4000.0;
            ShearStrengthReductionFactor = 1.0;
        }
    }
}

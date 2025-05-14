using Core.Models;
using Core.Models.Properties;
using System.Collections.Generic;
using System.Text;

public class MaterialsImport
{
    public string ConvertToE2K(List<Material> materials)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("$ MATERIAL PROPERTIES");

        foreach (var material in materials)
        {
            // Basic material definition using enum type
            sb.AppendLine($"\tMATERIAL \"{material.Name}\" TYPE \"{material.Type}\" GRADE \"{GetMaterialGrade(material)}\" " +
                         $"WEIGHTPERVOLUME {GetMaterialWeight(material)}");

            // Material properties based on enum type
            switch (material.Type)
            {
                case MaterialType.Steel:
                    // Add steel-specific properties
                    double e = material.ElasticModulus;
                    double u = material.PoissonsRatio;
                    double a = material.CoefficientOfThermalExpansion;

                    sb.AppendLine($"\tMATERIAL \"{material.Name}\" SYMTYPE \"{material.DirectionalSymmetryType}\" E {e} U {u} A {a}");

                    // Access SteelProps for steel-specific values
                    double fy = material.SteelProps?.Fy ?? 50000.0;
                    double fu = material.SteelProps?.Fu ?? 65000.0;

                    sb.AppendLine($"\tMATERIAL \"{material.Name}\" FY {fy} FU {fu} FYE {material.SteelProps?.Fye ?? fy * 1.1} FUE {material.SteelProps?.Fue ?? fu * 1.1}");
                    break;

                case MaterialType.Concrete:
                    // Add concrete-specific properties
                    e = material.ElasticModulus;
                    u = material.PoissonsRatio;
                    a = material.CoefficientOfThermalExpansion;

                    sb.AppendLine($"\tMATERIAL \"{material.Name}\" SYMTYPE \"{material.DirectionalSymmetryType}\" E {e} U {u} A {a}");

                    // Access ConcreteProps for concrete-specific values
                    double fc = material.ConcreteProps?.Fc ?? 4000.0;

                    sb.AppendLine($"\tMATERIAL \"{material.Name}\" FC {fc}");
                    sb.AppendLine($"\tMATERIAL \"{material.Name}\" TIMEDEPCONCCODE \"CEBFIP90\"");
                    break;
            }
        }

        return sb.ToString();
    }

    private string GetMaterialGrade(Material material)
    {
        switch (material.Type)
        {
            case MaterialType.Steel:
                return material.SteelProps?.Grade ?? "50";
            case MaterialType.Concrete:
                return material.ConcreteProps?.Grade ?? "f'c 4 ksi";
            default:
                return "Standard";
        }
    }

    private double GetMaterialWeight(Material material)
    {
        // Get weight density from material properties
        double density = material.WeightPerUnitVolume;
        return density / 1728.0; // convert from pcf to lb/in³
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Properties;

namespace ETABS.Export.Properties
{
    /// <summary>
    /// Converts Core Material objects to ETABS E2K format text
    /// </summary>
    public class MaterialsExport
    {
        /// <summary>
        /// Converts a collection of Material objects to E2K format text
        /// </summary>
        /// <param name="materials">Collection of Material objects</param>
        /// <returns>E2K format text for materials</returns>
        public string ConvertToE2K(List<Material> materials)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Materials Section Header
            sb.AppendLine("$ MATERIAL PROPERTIES");

            foreach (var material in materials)
            {
                // Basic material definition
                sb.AppendLine($"\tMATERIAL \"{material.Name}\" TYPE \"{material.Type}\" GRADE \"Grade {GetMaterialGrade(material)}\" " +
                             $"WEIGHTPERVOLUME {GetMaterialWeight(material)}");

                // Material properties based on type
                switch (material.Type.ToLower())
                {
                    case "steel":
                        // Add steel-specific properties
                        double e = GetDesignValue(material, "elasticModulus", 29000000.0);
                        double u = GetDesignValue(material, "poissonsRatio", 0.3);
                        double a = GetDesignValue(material, "thermalCoeff", 6.5e-6);

                        sb.AppendLine($"\tMATERIAL \"{material.Name}\" SYMTYPE \"Isotropic\" E {e} U {u} A {a}");

                        double fy = GetDesignValue(material, "fy", 50000.0);
                        double fu = GetDesignValue(material, "fu", 65000.0);

                        sb.AppendLine($"\tMATERIAL \"{material.Name}\" FY {fy} FU {fu} FYE {fy * 1.1} FUE {fu * 1.1}");
                        break;

                    case "concrete":
                        // Add concrete-specific properties
                        e = GetDesignValue(material, "elasticModulus", 3600000.0);
                        u = GetDesignValue(material, "poissonsRatio", 0.2);
                        a = GetDesignValue(material, "thermalCoeff", 5.5e-6);

                        sb.AppendLine($"\tMATERIAL \"{material.Name}\" SYMTYPE \"Isotropic\" E {e} U {u} A {a}");

                        double fc = GetDesignValue(material, "fc", 4000.0);

                        sb.AppendLine($"\tMATERIAL \"{material.Name}\" FC {fc}");
                        sb.AppendLine($"\tMATERIAL \"{material.Name}\" TIMEDEPCONCCODE \"CEBFIP90\"");
                        break;
                }
            }

            return sb.ToString();
        }

        private string GetMaterialGrade(Material material)
        {
            // Try to get grade from material properties
            if (material.DesignData.ContainsKey("grade") && material.DesignData["grade"] is string grade)
            {
                return grade;
            }

            // Default grades based on material type
            switch (material.Type.ToLower())
            {
                case "steel":
                    return "50";
                case "concrete":
                    if (material.DesignData.ContainsKey("fc") && material.DesignData["fc"] is double fc)
                        return "f'c " + (fc / 1000) + " ksi";
                    else
                        return "Unknown concrete grade";
                default:
                    return "Standard";
            }
        }

        private double GetMaterialWeight(Material material)
        {
            // Get weight density from material properties
            if (material.DesignData.ContainsKey("weightDensity") && material.DesignData["weightDensity"] is double weight)
            {
                return weight / 1728.0; // convert from pcf to lb/in³
            }

            // Default weights based on material type
            switch (material.Type.ToLower())
            {
                case "steel":
                    return 0.2835648; // lb/in³
                case "concrete":
                    return 0.0868055; // lb/in³
                default:
                    return 0.1; // Default value
            }
        }

        private double GetDesignValue(Material material, string propertyName, double defaultValue)
        {
            if (material.DesignData.ContainsKey(propertyName) && material.DesignData[propertyName] is double value)
            {
                return value;
            }
            return defaultValue;
        }
    }
}
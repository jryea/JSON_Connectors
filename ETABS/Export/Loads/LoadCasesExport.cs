using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Loads;

namespace ETABS.Export.Loads
{
    // Converts Load objects to ETABS E2K Load Cases
    public class LoadCasesExport
    {
        /// <summary>
        /// Converts LoadContainer to E2K format text for Load Cases section
        /// </summary>
        /// <param name="loads">LoadContainer object</param>
        /// <returns>E2K format text for load cases</returns>
        public string ConvertToE2K(LoadContainer loads)
        {
            StringBuilder sb = new StringBuilder();

            // Add load cases section
            sb.AppendLine("$ LOAD CASES");

            // Add modal analysis case
            sb.AppendLine("  LOADCASE \"MODAL\"  TYPE  \"Modal - Eigen\"  INITCOND  \"PRESET\"  ");
            sb.AppendLine("  LOADCASE \"MODAL\"  MAXMODES  12 MINMODES  1 EIGENSHIFTFREQ  0 EIGENCUTOFF  0 EIGENTOL  1E-09 ");

            if (loads.LoadDefinitions == null || loads.LoadDefinitions.Count == 0)
            {
                // Add default load cases if none are provided
                sb.AppendLine("  LOADCASE \"SW\"  TYPE  \"Linear Static\"  INITCOND  \"PRESET\"  ");
                sb.AppendLine("  LOADCASE \"SW\"  LOADPAT  \"SW\"  SF  1 ");
                sb.AppendLine("  LOADCASE \"LIVE\"  TYPE  \"Linear Static\"  INITCOND  \"PRESET\"  ");
                sb.AppendLine("  LOADCASE \"LIVE\"  LOADPAT  \"LIVE\"  SF  1 ");
                sb.AppendLine("  LOADCASE \"SDL\"  TYPE  \"Linear Static\"  INITCOND  \"PRESET\"  ");
                sb.AppendLine("  LOADCASE \"SDL\"  LOADPAT  \"SDL\"  SF  1 ");
                return sb.ToString();
            }

            // Process load definitions to create load cases
            foreach (var loadDef in loads.LoadDefinitions)
            {
                // Check if this is a seismic or wind load that needs special handling
                if (loadDef.Type?.ToLower() == "seismic" || loadDef.Type?.ToLower() == "earthquake")
                {
                    FormatSeismicLoadCase(sb, loadDef);
                }
                else if (loadDef.Type?.ToLower() == "wind")
                {
                    FormatWindLoadCase(sb, loadDef);
                }
                else
                {
                    // Standard linear static load case
                    sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  TYPE  \"Linear Static\"  INITCOND  \"PRESET\"  ");
                    sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  LOADPAT  \"{loadDef.Name}\"  SF  1 ");
                }
            }

            return sb.ToString();
        }

        private void FormatSeismicLoadCase(StringBuilder sb, LoadDefinition loadDef)
        {
            // Format a seismic load case
            sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  TYPE  \"Linear Static\"  INITCOND  \"PRESET\"  ");
            sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  LOADPAT  \"{loadDef.Name}\"  SF  1 ");

            // If seismic details are available, add them
            if (loadDef.Properties != null)
            {
                string direction = GetPropertyStringValue(loadDef.Properties, "direction", "X");
                string code = GetPropertyStringValue(loadDef.Properties, "code", "ASCE 7-16");
                double eccentricity = GetPropertyDoubleValue(loadDef.Properties, "eccentricity", 0.05);

                sb.AppendLine($"  SEISMIC \"{loadDef.Name}\"  \"{code}\"    DIR \"{direction} {direction}+ECC {direction}-ECC\"  ECC {eccentricity}  " +
                              "TOPSTORY \"Story16\"    BOTTOMSTORY \"Base\"   PERIODTYPE \"PROGCALC\"   CTTYPE 3  " +
                              "R 6  OMEGA 2.5  CD 5.5  I 1  SITECLASS \"E\"    Ss 1.5  S1 0.6  TL 12");
            }
        }

        private void FormatWindLoadCase(StringBuilder sb, LoadDefinition loadDef)
        {
            // Format a wind load case
            sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  TYPE  \"Linear Static\"  INITCOND  \"PRESET\"  ");
            sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  LOADPAT  \"{loadDef.Name}\"  SF  1 ");

            // Add wind load specific parameters if available
            if (loadDef.Properties != null)
            {
                // Could add wind-specific parameters here based on available properties
            }
        }

        private string GetPropertyStringValue(Dictionary<string, object> properties, string key, string defaultValue)
        {
            if (properties.ContainsKey(key) && properties[key] is string value)
            {
                return value;
            }
            return defaultValue;
        }

        private double GetPropertyDoubleValue(Dictionary<string, object> properties, string key, double defaultValue)
        {
            if (properties.ContainsKey(key))
            {
                if (properties[key] is double doubleValue)
                {
                    return doubleValue;
                }

                // Try to convert from string or int
                if (properties[key] is string strValue && double.TryParse(strValue, out double result))
                {
                    return result;
                }

                if (properties[key] is int intValue)
                {
                    return intValue;
                }
            }
            return defaultValue;
        }
    }
}
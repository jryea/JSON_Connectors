using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Core.Models.Loads;

namespace ETABS.Export.Loads
{
    /// <summary>
    /// Converts Core LoadCombination objects to ETABS E2K format text
    /// </summary>
    public class LoadCombinationsExport
    {
        /// <summary>
        /// Converts LoadContainer to E2K format text for Load Combinations section
        /// </summary>
        /// <param name="loads">LoadContainer object</param>
        /// <returns>E2K format text for load combinations</returns>
        public string ConvertToE2K(LoadContainer loads)
        {
            StringBuilder sb = new StringBuilder();

            // Add load combinations section
            sb.AppendLine("$ LOAD COMBINATIONS");

            if (loads.LoadCombinations == null || loads.LoadCombinations.Count == 0)
            {
                // If no combinations are defined, create a basic EQ envelope combination as default
                sb.AppendLine("  COMBO \"EQ ENVELOPE\"  TYPE \"Envelope\"  ");

                // Find seismic loads from load definitions if available
                var seismicLoads = loads.LoadDefinitions?.Where(ld =>
                    ld.Type?.ToLower() == "seismic" ||
                    ld.Type?.ToLower() == "earthquake" ||
                    ld.Name.ToUpper().Contains("EQ"))
                    .ToList();

                if (seismicLoads != null && seismicLoads.Count > 0)
                {
                    foreach (var load in seismicLoads)
                    {
                        sb.AppendLine($"  COMBO \"EQ ENVELOPE\"  LOADCASE \"{load.Name}\"  SF 1 ");
                    }
                }
                else
                {
                    // Add placeholder earthquake loads if no seismic definitions exist
                    sb.AppendLine("  COMBO \"EQ ENVELOPE\"  LOADCASE \"EQX\"  SF 1 ");
                    sb.AppendLine("  COMBO \"EQ ENVELOPE\"  LOADCASE \"EQY\"  SF 1 ");
                }

                return sb.ToString();
            }

            // Process user-defined load combinations
            Dictionary<string, List<LoadComboComponent>> combos = new Dictionary<string, List<LoadComboComponent>>();

            // Group combinations by their names/types
            foreach (var combo in loads.LoadCombinations)
            {
                // Find the load definition referenced by this combination
                var loadDef = loads.LoadDefinitions.FirstOrDefault(ld => ld.Id == combo.LoadDefinitionId);
                if (loadDef == null) continue;

                string comboName = combo.Id; // Use combo ID as name by default

                // If the combo has properties that specify a name, use that instead
                if (loadDef.Properties != null)
                {
                    if (loadDef.Properties.ContainsKey("combinationName") && loadDef.Properties["combinationName"] is string name)
                    {
                        comboName = name;
                    }
                    else if (loadDef.Name != null)
                    {
                        comboName = loadDef.Name;
                    }
                }

                // Create a component for this load case with scale factor
                double scaleFactor = 1.0;
                if (loadDef.Properties != null &&
                    loadDef.Properties.ContainsKey("scaleFactor") &&
                    loadDef.Properties["scaleFactor"] is double sf)
                {
                    scaleFactor = sf;
                }

                // Create the load combination component
                var component = new LoadComboComponent
                {
                    LoadCaseName = loadDef.Name,
                    ScaleFactor = scaleFactor
                };

                // Add to the dictionary, creating a new list if needed
                if (!combos.ContainsKey(comboName))
                {
                    combos[comboName] = new List<LoadComboComponent>();
                }
                combos[comboName].Add(component);
            }

            // Now format and write each combination
            foreach (var combo in combos)
            {
                // Determine combination type - default to "Linear Add"
                string comboType = "Linear Add";

                // Check if this looks like an envelope combination
                if (combo.Key.ToLower().Contains("envelope") ||
                    combo.Key.ToLower().Contains("max") ||
                    combo.Key.ToLower().Contains("min"))
                {
                    comboType = "Envelope";
                }

                // Add combination type line
                sb.AppendLine($"  COMBO \"{combo.Key}\"  TYPE \"{comboType}\"  ");

                // Add each component
                foreach (var component in combo.Value)
                {
                    sb.AppendLine($"  COMBO \"{combo.Key}\"  LOADCASE \"{component.LoadCaseName}\"  SF {component.ScaleFactor} ");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Helper class for load combination components
        /// </summary>
        private class LoadComboComponent
        {
            public string LoadCaseName { get; set; }
            public double ScaleFactor { get; set; }
        }
    }
}
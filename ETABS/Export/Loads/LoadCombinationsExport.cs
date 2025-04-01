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
        /// Converts LoadContainer with LoadCombinations to E2K format text for LOAD COMBINATIONS section
        /// </summary>
        /// <param name="loadContainer">LoadContainer containing load combinations</param>
        /// <returns>E2K format text for load combinations</returns>
        public string ConvertToE2K(LoadContainer loadContainer)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Load Combinations Header
            sb.AppendLine("$ LOAD COMBINATIONS");

            // If no load combinations exist, create a default EQ ENVELOPE combination
            if (loadContainer == null || loadContainer.LoadCombinations == null || !loadContainer.LoadCombinations.Any())
            {
                // Create a default envelope for EQX and EQY
                sb.AppendLine("  COMBO \"EQ ENVELOPE\"  TYPE \"Envelope\"  ");
                sb.AppendLine("  COMBO \"EQ ENVELOPE\"  LOADCASE \"EQX\"  SF 1 ");
                sb.AppendLine("  COMBO \"EQ ENVELOPE\"  LOADCASE \"EQY\"  SF 1 ");

                return sb.ToString();
            }

            // Dictionary to store load definition names by ID for reference
            Dictionary<string, string> loadDefNames = new Dictionary<string, string>();

            // Build the dictionary if load definitions exist
            if (loadContainer.LoadDefinitions != null)
            {
                foreach (var loadDef in loadContainer.LoadDefinitions)
                {
                    if (!string.IsNullOrEmpty(loadDef.Id) && !string.IsNullOrEmpty(loadDef.Name))
                    {
                        loadDefNames[loadDef.Id] = loadDef.Name;
                    }
                }
            }

            // Process each load combination
            foreach (var loadCombo in loadContainer.LoadCombinations)
            {
                // Generate a name for the combination if not specified
                string comboName = GetCombinationName(loadCombo);

                // Determine combination type (default to linear)
                string comboType = DetermineCombinationType(loadCombo);

                // First line: Basic combination definition
                sb.AppendLine($"  COMBO \"{comboName}\"  TYPE \"{comboType}\"  ");

                // Process each load definition in the combination
                if (loadCombo.LoadDefinitionIds != null)
                {
                    foreach (var loadDefId in loadCombo.LoadDefinitionIds)
                    {
                        // Try to get the load definition name from our dictionary
                        string loadDefName = "Unknown";
                        if (loadDefNames.ContainsKey(loadDefId))
                        {
                            loadDefName = loadDefNames[loadDefId];
                        }

                        // Add the load definition to the combination with scale factor 1
                        sb.AppendLine($"  COMBO \"{comboName}\"  LOADCASE \"{loadDefName}\"  SF 1 ");
                    }
                }
            }

            // Always add a default EQ ENVELOPE combination if it doesn't exist
            bool hasEqEnvelope = loadContainer.LoadCombinations.Any(lc =>
                (lc.LoadDefinitionIds?.Count(id =>
                    loadDefNames.ContainsKey(id) &&
                    (loadDefNames[id].ToLower().Contains("eq") ||
                     loadDefNames[id].ToLower().Contains("seismic"))) ?? 0) > 1);

            if (!hasEqEnvelope)
            {
                sb.AppendLine("  COMBO \"EQ ENVELOPE\"  TYPE \"Envelope\"  ");
                sb.AppendLine("  COMBO \"EQ ENVELOPE\"  LOADCASE \"EQX\"  SF 1 ");
                sb.AppendLine("  COMBO \"EQ ENVELOPE\"  LOADCASE \"EQY\"  SF 1 ");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a name for a load combination
        /// </summary>
        /// <param name="loadCombo">LoadCombination object</param>
        /// <returns>Name for the load combination</returns>
        private string GetCombinationName(LoadCombination loadCombo)
        {
            // Use the last portion of the ID if no other identifier is available
            if (loadCombo.Id != null && loadCombo.Id.Contains("-"))
            {
                string idEnd = loadCombo.Id.Split('-').Last();
                return $"COMBO_{idEnd}";
            }

            // If all else fails, generate a random identifier
            return $"COMBO_{Guid.NewGuid().ToString().Substring(0, 8)}";
        }

        /// <summary>
        /// Determines the type of combination (Linear, Envelope, etc.)
        /// </summary>
        /// <param name="loadCombo">LoadCombination object</param>
        /// <returns>ETABS combination type</returns>
        private string DetermineCombinationType(LoadCombination loadCombo)
        {
            // For now, keep it simple and just use Linear Add
            // In a more complete implementation, this would check for properties 
            // in the LoadCombination to determine if it's an envelope, etc.
            return "Linear Add";
        }
    }
}
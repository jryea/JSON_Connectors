using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Models.Loads;
using Core.Utilities;

namespace ETABS.Import.Loads
{
    // Imports load combination definitions from ETABS E2K file
    public class ETABSToLoadCombination
    {
        // Dictionary to map load pattern names to load definition IDs
        private Dictionary<string, string> _loadDefIdsByName = new Dictionary<string, string>();

        // Sets the load definition name to ID mapping for reference when creating load combinations
        public void SetLoadDefinitions(IEnumerable<LoadDefinition> loadDefinitions)
        {
            _loadDefIdsByName.Clear();
            foreach (var loadDef in loadDefinitions)
            {
                if (!string.IsNullOrEmpty(loadDef.Name))
                {
                    _loadDefIdsByName[loadDef.Name] = loadDef.Id;
                }
            }
        }

        // Imports load combinations from E2K LOAD COMBINATIONS section
        public List<LoadCombination> Import(string loadCombosSection, IEnumerable<LoadDefinition> loadDefinitions)
        {
            // Set up load definitions mapping
            SetLoadDefinitions(loadDefinitions);

            var loadCombinations = new Dictionary<string, LoadCombination>();

            if (string.IsNullOrWhiteSpace(loadCombosSection))
                return new List<LoadCombination>();

            // Regular expression to match load combination definition line
            // Format: COMBO "COMBO1" TYPE "Linear Add"
            var comboDefPattern = new Regex(@"^\s*COMBO\s+""([^""]+)""\s+TYPE\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Pattern for load case inclusion in combo
            // Format: COMBO "COMBO1" LOADCASE "SW" SF 1
            var comboLoadCasePattern = new Regex(@"^\s*COMBO\s+""([^""]+)""\s+LOADCASE\s+""([^""]+)""\s+SF\s+([\d\.E\+\-]+)",
                RegexOptions.Multiline);

            // First, identify all load combinations
            var comboDefMatches = comboDefPattern.Matches(loadCombosSection);
            foreach (Match match in comboDefMatches)
            {
                if (match.Groups.Count >= 3)
                {
                    string comboName = match.Groups[1].Value;
                    string comboType = match.Groups[2].Value;

                    // Create a new load combination if it doesn't exist
                    if (!loadCombinations.ContainsKey(comboName))
                    {
                        var loadCombo = new LoadCombination
                        {
                            Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_COMBINATION),
                            LoadDefinitionIds = new List<string>()
                        };

                        loadCombinations[comboName] = loadCombo;
                    }
                }
            }

            // Then, add load cases to each combination
            var comboLoadCaseMatches = comboLoadCasePattern.Matches(loadCombosSection);
            foreach (Match match in comboLoadCaseMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string comboName = match.Groups[1].Value;
                    string loadCaseName = match.Groups[2].Value;
                    double scaleFactor = Convert.ToDouble(match.Groups[3].Value);

                    // Find the matching load definition
                    if (_loadDefIdsByName.TryGetValue(loadCaseName, out string loadDefId))
                    {
                        // Find the load combination
                        if (loadCombinations.TryGetValue(comboName, out LoadCombination loadCombo))
                        {
                            // Add the load definition ID to the load combination if not already present
                            if (!loadCombo.LoadDefinitionIds.Contains(loadDefId))
                            {
                                loadCombo.LoadDefinitionIds.Add(loadDefId);
                            }

                            // TODO: If the LoadCombination class is extended to include scale factors,
                            // store the scale factor here
                        }
                    }
                }
            }

            return new List<LoadCombination>(loadCombinations.Values);
        }
    }
}
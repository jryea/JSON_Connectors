using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models.Loads;
using Core.Utilities;

namespace ETABS.Import.Loads
{
    // Imports load definition from ETABS E2K file
    public class ETABSToLoadDefinition
    {
        // Imports load definitions from E2K LOAD PATTERNS section
        public List<LoadDefinition> Import(string loadPatternsSection)
        {
            var loadDefinitions = new Dictionary<string, LoadDefinition>();

            if (string.IsNullOrWhiteSpace(loadPatternsSection))
                return new List<LoadDefinition>();

            // Regular expression to match load pattern definition
            // Format: LOADPATTERN "SW" TYPE "Dead" SELFWEIGHT 1
            var loadPatternPattern = new Regex(@"^\s*LOADPATTERN\s+""([^""]+)""\s+TYPE\s+""([^""]+)""\s+SELFWEIGHT\s+([\d\.]+)",
                RegexOptions.Multiline);

            // Pattern for seismic parameters (optional)
            var seismicPattern = new Regex(@"^\s*SEISMIC\s+""([^""]+)""\s+""([^""]+)""\s+DIR\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Process load pattern definitions
            var loadPatternMatches = loadPatternPattern.Matches(loadPatternsSection);
            foreach (Match match in loadPatternMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string name = match.Groups[1].Value;
                    string type = match.Groups[2].Value;
                    double selfWeight = Convert.ToDouble(match.Groups[3].Value);

                    // Create load definition
                    var loadDef = new LoadDefinition
                    {
                        Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
                        Name = name,
                        Type = ConvertLoadType(type),
                        SelfWeight = selfWeight
                    };

                    loadDefinitions[name] = loadDef;
                }
            }

            // Process seismic parameters
            var seismicMatches = seismicPattern.Matches(loadPatternsSection);
            foreach (Match match in seismicMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string loadName = match.Groups[1].Value;
                    string code = match.Groups[2].Value;
                    string direction = match.Groups[3].Value;

                    // Update existing load definition if it exists
                    if (loadDefinitions.TryGetValue(loadName, out LoadDefinition loadDef))
                    {
                        // Store seismic parameters in extended properties
                        // We would need to extend the LoadDefinition class to include these properties
                        // For now, we'll just set the type to ensure it's recognized as seismic
                        loadDef.Type = "Seismic";
                    }
                }
            }

            return new List<LoadDefinition>(loadDefinitions.Values);
        }

        // Converts ETABS load type to model load type
        private string ConvertLoadType(string etabsType)
        {
            switch (etabsType.ToLower())
            {
                case "dead":
                    return "Dead";
                case "live":
                    return "Live";
                case "wind":
                    return "Wind";
                case "snow":
                    return "Snow";
                case "seismic":
                    return "Seismic";
                case "temperature":
                    return "Temperature";
                default:
                    return etabsType; // Keep original if not recognized
            }
        }
    }
}
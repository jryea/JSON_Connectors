using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models.Loads;
using Core.Utilities;

namespace ETABS.Export.Loads
{
    // Imports load definition from ETABS E2K file
    public class LoadDefinitionExport
    {
        // Imports load definitions from E2K LOAD PATTERNS section
        public List<LoadDefinition> Export(string loadPatternsSection)
        {
            var loadDefinitions = new Dictionary<string, LoadDefinition>();

            if (string.IsNullOrWhiteSpace(loadPatternsSection))
                return new List<LoadDefinition>();

            // Regular expression to match load pattern definition
            var loadPatternPattern = new Regex(@"^\s*LOADPATTERN\s+""([^""]+)""\s+TYPE\s+""([^""]+)""\s+SELFWEIGHT\s+([\d\.]+)",
                RegexOptions.Multiline);

            // Process load pattern definitions
            var loadPatternMatches = loadPatternPattern.Matches(loadPatternsSection);
            foreach (Match match in loadPatternMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string name = match.Groups[1].Value;
                    string typeStr = match.Groups[2].Value;
                    double selfWeight = Convert.ToDouble(match.Groups[3].Value);

                    // Create load definition with enum LoadType
                    var loadDef = new LoadDefinition
                    {
                        Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
                        Name = name,
                        Type = ConvertToLoadType(typeStr),
                        SelfWeight = selfWeight
                    };

                    loadDefinitions[name] = loadDef;
                }
            }

            return new List<LoadDefinition>(loadDefinitions.Values);
        }

        // Converts ETABS load type string to model LoadType enum
        private LoadType ConvertToLoadType(string etabsType)
        {
            switch (etabsType.ToLower())
            {
                case "dead":
                    return LoadType.Dead;
                case "live":
                    return LoadType.Live;
                case "wind":
                    return LoadType.Wind;
                case "snow":
                    return LoadType.Snow;
                case "seismic":
                    return LoadType.Seismic;
                default:
                    return LoadType.Other;
            }
        }
    }
}
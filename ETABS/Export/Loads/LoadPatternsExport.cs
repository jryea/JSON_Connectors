using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Loads;

namespace ETABS.Export.Loads
{
    // Converts Core Load objects to ETABS E2K format text for Load Patterns section
    public class LoadPatternsExport
    {
        // Converts LoadContainer to E2K format text for Load Patterns section
        public string ConvertToE2K(LoadContainer loads)
        {
            StringBuilder sb = new StringBuilder();

            // Add load patterns section
            sb.AppendLine("$ LOAD PATTERNS");

            if (loads.LoadDefinitions == null || loads.LoadDefinitions.Count == 0)
            {
                // Add default load patterns if none are provided
                sb.AppendLine("  LOADPATTERN \"SW\"  TYPE  \"Dead\"  SELFWEIGHT  1");
                sb.AppendLine("  LOADPATTERN \"LIVE\"  TYPE  \"Live\"  SELFWEIGHT  0");
                sb.AppendLine("  LOADPATTERN \"SDL\"  TYPE  \"Dead\"  SELFWEIGHT  0");
                return sb.ToString();
            }

            foreach (var loadDef in loads.LoadDefinitions)
            {
                string loadType = ConvertLoadTypeToE2K(loadDef.Type);
                double selfWeight = loadDef.SelfWeight;
                sb.AppendLine($"  LOADPATTERN \"{loadDef.Name}\"  TYPE  \"{loadType}\"  SELFWEIGHT  {selfWeight}");
            }

            return sb.ToString();
        }

        private string ConvertLoadTypeToE2K(string coreLoadType)
        {
            // Convert model load type to ETABS format
            if (string.IsNullOrEmpty(coreLoadType))
                return "Dead";

            switch (coreLoadType.ToLower())
            {
                case "dead":
                    return "Dead";
                case "superimposed dead":
                case "sdl":
                    return "SuperDead";
                case "live":
                    return "Live";
                case "reducible live":
                    return "ReducibleLive";
                case "roof live":
                    return "RoofLive";
                case "wind":
                    return "Wind";
                case "seismic":
                case "earthquake":
                    return "Seismic";
                case "snow":
                    return "Snow";
                case "rain":
                    return "Rain";
                case "temperature":
                    return "Temperature";
                case "settlement":
                    return "Settlement";
                default:
                    return "Other"; // Default load type
            }
        }
    }
}
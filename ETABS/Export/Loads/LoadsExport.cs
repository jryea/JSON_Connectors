using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Loads;

namespace ETABS.Export.Loads
{
    /// <summary>
    /// Converts Core Load objects to ETABS E2K format text
    /// </summary>
    public class LoadsExport
    {
        /// <summary>
        /// Converts LoadContainer to E2K format text
        /// </summary>
        /// <param name="loads">LoadContainer object</param>
        /// <returns>E2K format text for loads</returns>
        public string ConvertToE2K(LoadContainer loads)
        {
            StringBuilder sb = new StringBuilder();

            // Add load patterns section
            sb.AppendLine("$ LOAD PATTERNS");
            foreach (var loadDef in loads.LoadDefinitions)
            {
                string loadType = ConvertLoadTypeToE2K(loadDef.Type);
                sb.AppendLine($"LOADPATTERN \"{loadDef.Name}\"  TYPE  \"{loadType}\"  SELFWEIGHT  {(loadDef.Properties.ContainsKey("selfWeight") ? loadDef.Properties["selfWeight"] : 0)}");
            }

            // Add surface loads section
            sb.AppendLine();
            sb.AppendLine("$ SHELL UNIFORM LOAD SETS");
            foreach (var surfLoad in loads.SurfaceLoads)
            {
                // Find the dead and live load definitions
                var deadLoadDef = loads.LoadDefinitions.Find(ld => ld.Id == surfLoad.DeadId);
                var liveLoadDef = loads.LoadDefinitions.Find(ld => ld.Id == surfLoad.LiveId);

                if (deadLoadDef != null && liveLoadDef != null)
                {
                    sb.AppendLine($"SHELLUNIFORMLOADSET \"{surfLoad.Id}\"  LOADPAT \"{deadLoadDef.Name}\"  VALUE {GetLoadValue(deadLoadDef)}");
                    sb.AppendLine($"SHELLUNIFORMLOADSET \"{surfLoad.Id}\"  LOADPAT \"{liveLoadDef.Name}\"  VALUE {GetLoadValue(liveLoadDef)}");
                }
            }

            // Add load combinations section
            sb.AppendLine();
            sb.AppendLine("$ LOAD COMBINATIONS");
            foreach (var combo in loads.LoadCombinations)
            {
                var loadDef = loads.LoadDefinitions.Find(ld => ld.Id == combo.LoadDefinitionId);
                if (loadDef != null)
                {
                    sb.AppendLine($"COMBO \"{loadDef.Name}\"  TYPE \"Envelope\"  ");
                    sb.AppendLine($"COMBO \"{loadDef.Name}\"  LOADCASE \"{loadDef.Name}\"  SF 1 ");
                }
            }

            return sb.ToString();
        }

        private string ConvertLoadTypeToE2K(string coreLoadType)
        {
            // Convert your model load type to ETABS format
            switch (coreLoadType.ToLower())
            {
                case "dead":
                    return "Dead";
                case "live":
                    return "Live";
                case "wind":
                    return "Wind";
                case "seismic":
                    return "Seismic";
                case "snow":
                    return "Snow";
                case "temperature":
                    return "Temperature";
                default:
                    return "Other"; // Default load type
            }
        }

        private double GetLoadValue(LoadDefinition loadDef)
        {
            if (loadDef.Properties.ContainsKey("value") && loadDef.Properties["value"] is double value)
            {
                return value;
            }
            return 0.0;
        }
    }
}
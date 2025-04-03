using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Loads;
using Core.Models.Properties;

namespace ETABS.Export.Loads
{
    // Converts SurfaceLoad objects to E2K Shell Uniform Load Sets format
    public class ShellPropsToETABS
    {
        /// <summary>
        /// Converts a collection of SurfaceLoad objects to E2K format text
        /// </summary>
        /// <param name="surfaceLoads">Collection of SurfaceLoad objects</param>
        /// <param name="loadDefinitions">Collection of LoadDefinition objects</param>
        /// <returns>E2K format text for shell uniform load sets</returns>
        public string ConvertToE2K(IEnumerable<SurfaceLoad> surfaceLoads, IEnumerable<LoadDefinition> loadDefinitions)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Shell Uniform Load Sets Header
            sb.AppendLine("$ SHELL UNIFORM LOAD SETS");

            if (surfaceLoads == null || !surfaceLoads.Any() || loadDefinitions == null || !loadDefinitions.Any())
            {
                // Add default load set if none are provided
                sb.AppendLine("  SHELLUNIFORMLOADSET \"0 TYPICAL\"  LOADPAT \"SDL\"  VALUE 0.1388889");
                sb.AppendLine("  SHELLUNIFORMLOADSET \"0 TYPICAL\"  LOADPAT \"LIVE\"  VALUE 0.2777778");
                return sb.ToString();
            }

            // Create a dictionary to look up load definitions by ID
            Dictionary<string, LoadDefinition> loadDefsById = new Dictionary<string, LoadDefinition>();
            foreach (var loadDef in loadDefinitions)
            {
                if (!string.IsNullOrEmpty(loadDef.Id))
                {
                    loadDefsById[loadDef.Id] = loadDef;
                }
            }

            // Process each surface load
            foreach (var surfLoad in surfaceLoads)
            {
                string loadSetName = GetLoadSetName(surfLoad);

                // Process dead load
                if (!string.IsNullOrEmpty(surfLoad.DeadLoadId) && loadDefsById.ContainsKey(surfLoad.DeadLoadId))
                {
                    var deadLoadDef = loadDefsById[surfLoad.DeadLoadId];
                    double loadValue = GetLoadValue(deadLoadDef, "dead");
                    sb.AppendLine($"  SHELLUNIFORMLOADSET \"{loadSetName}\"  LOADPAT \"{deadLoadDef.Name}\"  VALUE {loadValue.ToString("0.#######")}");
                }
                else
                {
                    // Default dead load if not specified
                    sb.AppendLine($"  SHELLUNIFORMLOADSET \"{loadSetName}\"  LOADPAT \"SDL\"  VALUE 0.1388889");
                }

                // Process live load
                if (!string.IsNullOrEmpty(surfLoad.LiveLoadId) && loadDefsById.ContainsKey(surfLoad.LiveLoadId))
                {
                    var liveLoadDef = loadDefsById[surfLoad.LiveLoadId];
                    double loadValue = GetLoadValue(liveLoadDef, "live");
                    sb.AppendLine($"  SHELLUNIFORMLOADSET \"{loadSetName}\"  LOADPAT \"{liveLoadDef.Name}\"  VALUE {loadValue.ToString("0.#######")}");
                }
                else
                {
                    // Default live load if not specified
                    sb.AppendLine($"  SHELLUNIFORMLOADSET \"{loadSetName}\"  LOADPAT \"LIVE\"  VALUE 0.2777778");
                }
            }

            return sb.ToString();
        }

      
        // Gets a formatted name for the load set
        private string GetLoadSetName(SurfaceLoad surfLoad)
        {
            // If the surface load has a proper ID, use it
            if (!string.IsNullOrEmpty(surfLoad.Id))
            {
                if (surfLoad.Id.Contains("-"))
                {
                    // Extract the ID part after the hyphen
                    string idPart = surfLoad.Id.Split('-').Last();
                    return $"LS_{idPart}";
                }
                return $"LS_{surfLoad.Id}";
            }

            // Default name if none is available
            return "0 TYPICAL";
        }

        /// Gets a load value from a load definition with appropriate defaults by type
 
        private double GetLoadValue(LoadDefinition loadDef, string defaultType)
        {
            // Default values in psf (pounds per square foot)
            double defaultPsf = defaultType.ToLower() == "dead" ? 20.0 : 40.0;

            // Convert to ETABS units (lb/in²) - divide by 144
            double defaultValue = defaultPsf / 144.0;

            // In a more complete implementation, this would examine the load definition
            // to extract exact load values from its properties

            // Return the default value
            return defaultValue;
        }
    }
}
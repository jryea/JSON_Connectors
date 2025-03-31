using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Loads;
using Core.Models.Properties;

namespace ETABS.Export.Loads
{
    // Converts SurfaceLoad objects to E2K Shell Load Sets

    public class ShellPropsExport
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

            if (surfaceLoads == null || !surfaceLoads.Any())
            {
                // Add default load set if none are provided
                sb.AppendLine("  SHELLUNIFORMLOADSET \"0 TYPICAL\"  LOADPAT \"SDL\"  VALUE 0.1388889");
                sb.AppendLine("  SHELLUNIFORMLOADSET \"0 TYPICAL\"  LOADPAT \"LIVE\"  VALUE 0.2777778");
                return sb.ToString();
            }

            // Process each surface load
            foreach (var surfLoad in surfaceLoads)
            {
                string loadSetName = GetLoadSetName(surfLoad);

                // Find the dead and live load definitions
                var deadLoadDef = loadDefinitions.FirstOrDefault(ld => ld.Id == surfLoad.DeadId);
                var liveLoadDef = loadDefinitions.FirstOrDefault(ld => ld.Id == surfLoad.LiveId);

                // Format the dead load if available
                if (deadLoadDef != null)
                {
                    double deadLoadValue = GetLoadValue(deadLoadDef);
                    sb.AppendLine($"  SHELLUNIFORMLOADSET \"{loadSetName}\"  LOADPAT \"{deadLoadDef.Name}\"  VALUE {deadLoadValue}");
                }

                // Format the live load if available
                if (liveLoadDef != null)
                {
                    double liveLoadValue = GetLoadValue(liveLoadDef);
                    sb.AppendLine($"  SHELLUNIFORMLOADSET \"{loadSetName}\"  LOADPAT \"{liveLoadDef.Name}\"  VALUE {liveLoadValue}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats shell uniform load object assignments for affected areas
        /// </summary>
        /// <param name="surfaceLoads">Collection of SurfaceLoad objects</param>
        /// <returns>E2K format text for area loads</returns>
        public string FormatAreaLoads(IEnumerable<SurfaceLoad> surfaceLoads)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Shell Object Loads Header
            sb.AppendLine("$ SHELL OBJECT LOADS");

            if (surfaceLoads == null || !surfaceLoads.Any())
            {
                return sb.ToString();
            }

            // Process each surface load
            foreach (var surfLoad in surfaceLoads)
            {
                if (string.IsNullOrEmpty(surfLoad.LayoutTypeId))
                    continue;

                string loadSetName = GetLoadSetName(surfLoad);

                // Add area load assignment
                sb.AppendLine($"  AREALOAD  \"{surfLoad.LayoutTypeId}\"  \"Story1\"  TYPE \"UNIFLOADSET\"  \"{loadSetName}\"  ");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a formatted name for the load set
        /// </summary>
        /// <param name="surfLoad">SurfaceLoad object</param>
        /// <returns>Formatted load set name</returns>
        private string GetLoadSetName(SurfaceLoad surfLoad)
        {
            // If the surface load has an ID, use it, otherwise generate a default name
            return !string.IsNullOrEmpty(surfLoad.Id) ? surfLoad.Id : "0 TYPICAL";
        }

        /// <summary>
        /// Gets the load value from a load definition
        /// </summary>
        /// <param name="loadDef">LoadDefinition object</param>
        /// <returns>Load value</returns>
        private double GetLoadValue(LoadDefinition loadDef)
        {
            // Try to extract the load value from the load definition properties
            if (loadDef.Properties != null)
            {
                // Check for value property
                if (loadDef.Properties.ContainsKey("value"))
                {
                    if (loadDef.Properties["value"] is double doubleValue)
                    {
                        return doubleValue;
                    }

                    // Try to parse from string
                    if (loadDef.Properties["value"] is string stringValue &&
                        double.TryParse(stringValue, out double parsedValue))
                    {
                        return parsedValue;
                    }
                }

                // Check for magnitude property as alternative
                if (loadDef.Properties.ContainsKey("magnitude"))
                {
                    if (loadDef.Properties["magnitude"] is double magValue)
                    {
                        return magValue;
                    }

                    // Try to parse from string
                    if (loadDef.Properties["magnitude"] is string magString &&
                        double.TryParse(magString, out double parsedMag))
                    {
                        return parsedMag;
                    }
                }
            }

            // Default values based on load type
            if (loadDef.Type?.ToLower() == "dead" ||
                loadDef.Type?.ToLower() == "superimposed dead" ||
                loadDef.Type?.ToLower() == "sdl")
            {
                return 0.1388889; // 20 psf in ETABS units (0.1388889 lb/in²)
            }
            else if (loadDef.Type?.ToLower() == "live" ||
                     loadDef.Type?.ToLower() == "reducible live")
            {
                return 0.2777778; // 40 psf in ETABS units (0.2777778 lb/in²)
            }

            return 0.0;
        }
    }
}
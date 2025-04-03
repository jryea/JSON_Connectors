using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Core.Models.Loads;

namespace ETABS.Export.Loads
{
    /// <summary>
    /// Converts Core Load objects to ETABS E2K format text for Load Cases section
    /// </summary>
    public class LoadCasesToETABS
    {
        /// <summary>
        /// Converts LoadContainer with LoadDefinitions to E2K format text for the LOAD CASES section
        /// </summary>
        /// <param name="loadContainer">LoadContainer containing load definitions</param>
        /// <returns>E2K format text for load cases</returns>
        public string ConvertToE2K(LoadContainer loadContainer)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Load Cases Header
            sb.AppendLine("$ LOAD CASES");

            // Add Modal Analysis load case (required by ETABS)
            sb.AppendLine("  LOADCASE \"MODAL\"  TYPE  \"Modal - Eigen\"  INITCOND  \"PRESET\"  ");
            sb.AppendLine("  LOADCASE \"MODAL\"  MAXMODES  12 MINMODES  1 EIGENSHIFTFREQ  0 EIGENCUTOFF  0 EIGENTOL  1E-09 ");

            // Check if any load definitions exist, if not create defaults
            if (loadContainer == null || loadContainer.LoadDefinitions == null || !loadContainer.LoadDefinitions.Any())
            {
                // Add default load cases for standard load patterns
                AddDefaultLoadCase(sb, "SW", "Linear Static");
                AddDefaultLoadCase(sb, "LIVE", "Linear Static");
                AddDefaultLoadCase(sb, "SDL", "Linear Static");
                AddDefaultLoadCase(sb, "EQX", "Linear Static");
                AddDefaultLoadCase(sb, "EQY", "Linear Static");
            }
            else
            {
                // Process each load definition and create a corresponding load case
                foreach (var loadDef in loadContainer.LoadDefinitions)
                {
                    AddLoadCase(sb, loadDef);
                }

                // Make sure we have EQX and EQY load cases if needed
                if (!loadContainer.LoadDefinitions.Any(ld =>
                    ld.Type?.ToLower() == "seismic" &&
                    (ld.Name?.ToLower() == "eqx" || ld.Name?.ToLower() == "eq-x")))
                {
                    AddDefaultLoadCase(sb, "EQX", "Linear Static");
                }

                if (!loadContainer.LoadDefinitions.Any(ld =>
                    ld.Type?.ToLower() == "seismic" &&
                    (ld.Name?.ToLower() == "eqy" || ld.Name?.ToLower() == "eq-y")))
                {
                    AddDefaultLoadCase(sb, "EQY", "Linear Static");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Adds a load case entry for a specific load definition
        /// </summary>
        /// <param name="sb">StringBuilder to append to</param>
        /// <param name="loadDef">LoadDefinition to create a load case for</param>
        private void AddLoadCase(StringBuilder sb, LoadDefinition loadDef)
        {
            string analysisType = DetermineAnalysisType(loadDef.Type);

            // First line: Basic load case definition
            sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  TYPE  \"{analysisType}\"  INITCOND  \"PRESET\"  ");

            // Second line: Load pattern assignment (always 1:1 for simple cases)
            sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  LOADPAT  \"{loadDef.Name}\"  SF  1 ");
        }

        /// <summary>
        /// Adds a default load case entry
        /// </summary>
        /// <param name="sb">StringBuilder to append to</param>
        /// <param name="name">Name of the load case</param>
        /// <param name="analysisType">Type of analysis</param>
        private void AddDefaultLoadCase(StringBuilder sb, string name, string analysisType)
        {
            // First line: Basic load case definition
            sb.AppendLine($"  LOADCASE \"{name}\"  TYPE  \"{analysisType}\"  INITCOND  \"PRESET\"  ");

            // Second line: Load pattern assignment (always 1:1 for simple cases)
            sb.AppendLine($"  LOADCASE \"{name}\"  LOADPAT  \"{name}\"  SF  1 ");
        }

        /// <summary>
        /// Determines the appropriate analysis type based on load type
        /// </summary>
        /// <param name="loadType">Type of load</param>
        /// <returns>ETABS analysis type</returns>
        private string DetermineAnalysisType(string loadType)
        {
            if (string.IsNullOrEmpty(loadType))
                return "Linear Static";

            switch (loadType.ToLower())
            {
                case "seismic":
                case "earthquake":
                case "eq":
                case "eqx":
                case "eqy":
                    return "Response Spectrum";

                case "modal":
                    return "Modal - Eigen";

                case "nonlinear static":
                case "pushover":
                    return "Nonlinear Static";

                case "buckling":
                    return "Buckling";

                case "time history":
                    return "Linear Direct Integration History";

                default:
                    // For all other load types, use Linear Static
                    return "Linear Static";
            }
        }
    }
}
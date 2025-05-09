using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Core.Models.Loads;

namespace ETABS.Import.Loads
{
    // Converts Core Load objects to ETABS E2K format text for Load Cases section
    public class LoadCasesImport
    {
        // Converts LoadContainer with LoadDefinitions to E2K format text for the LOAD CASES section
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
                ld.Type == LoadType.Seismic &&
                (ld.Name?.ToLower() == "eqx" || ld.Name?.ToLower() == "eq-x")))
                {
                    AddDefaultLoadCase(sb, "EQX", "Linear Static");
                }

                if (!loadContainer.LoadDefinitions.Any(ld =>
                    ld.Type == LoadType.Seismic &&
                    (ld.Name?.ToLower() == "eqy" || ld.Name?.ToLower() == "eq-y")))
                {
                    AddDefaultLoadCase(sb, "EQY", "Linear Static");
                }
            }

            return sb.ToString();
        }

        // Adds a load case entry for a specific load definition
        private void AddLoadCase(StringBuilder sb, LoadDefinition loadDef)
        {
            string analysisType = DetermineAnalysisType(loadDef.Type);

            // First line: Basic load case definition
            sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  TYPE  \"{analysisType}\"  INITCOND  \"PRESET\"  ");

            // Second line: Load pattern assignment (always 1:1 for simple cases)
            sb.AppendLine($"  LOADCASE \"{loadDef.Name}\"  LOADPAT  \"{loadDef.Name}\"  SF  1 ");
        }

        // Adds a default load case entry
        private void AddDefaultLoadCase(StringBuilder sb, string name, string analysisType)
        {
            // First line: Basic load case definition
            sb.AppendLine($"  LOADCASE \"{name}\"  TYPE  \"{analysisType}\"  INITCOND  \"PRESET\"  ");

            // Second line: Load pattern assignment (always 1:1 for simple cases)
            sb.AppendLine($"  LOADCASE \"{name}\"  LOADPAT  \"{name}\"  SF  1 ");
        }

        // Determines the appropriate analysis type based on load type
        private string DetermineAnalysisType(LoadType loadType)
        {
            switch (loadType)
            {
                case LoadType.Seismic:
                    return "Response Spectrum";
                default:
                    // For all other load types, use Linear Static
                    return "Linear Static";
            }
        }
    }
}
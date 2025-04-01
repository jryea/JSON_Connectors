using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Core.Models.Loads;

namespace ETABS.Export.Loads
{
    /// <summary>
    /// Converts Core LoadDefinition objects to ETABS E2K format text for Load Patterns section
    /// </summary>
    public class LoadPatternsExport
    {
        /// <summary>
        /// Converts LoadContainer with LoadDefinitions to E2K format text for the LOAD PATTERNS section
        /// </summary>
        /// <param name="loadContainer">LoadContainer containing load definitions</param>
        /// <returns>E2K format text for load patterns</returns>
        public string ConvertToE2K(LoadContainer loadContainer)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Load Patterns Header
            sb.AppendLine("$ LOAD PATTERNS");

            // Check if any load definitions exist, if not create default ones
            if (loadContainer == null || loadContainer.LoadDefinitions == null || !loadContainer.LoadDefinitions.Any())
            {
                // Add default load patterns
                sb.AppendLine("  LOADPATTERN \"SW\"  TYPE  \"Dead\"  SELFWEIGHT  1");
                sb.AppendLine("  LOADPATTERN \"LIVE\"  TYPE  \"Live\"  SELFWEIGHT  0");
                sb.AppendLine("  LOADPATTERN \"SDL\"  TYPE  \"Dead\"  SELFWEIGHT  0");
                sb.AppendLine("  LOADPATTERN \"EQX\"  TYPE  \"Seismic\"  SELFWEIGHT  0");
                sb.AppendLine("  LOADPATTERN \"EQY\"  TYPE  \"Seismic\"  SELFWEIGHT  0");

                // Default seismic parameters
                sb.AppendLine("  SEISMIC \"EQX\"  \"ASCE 7-16\"    DIR \"X X+ECC X-ECC\"  ECC 0.05  TOPSTORY \"Story4\"" +
                              "    BOTTOMSTORY \"Base\"   PERIODTYPE \"PROGCALC\"   CTTYPE 3  R 6  OMEGA 2.5  CD 5.5" +
                              "  I 1  SITECLASS \"E\"    Ss 1.5  S1 0.6  TL 12");
                sb.AppendLine("  SEISMIC \"EQY\"  \"ASCE 7-16\"    DIR \"Y Y+ECC Y-ECC\"  ECC 0.05  TOPSTORY \"Story4\"" +
                              "    BOTTOMSTORY \"Base\"   PERIODTYPE \"PROGCALC\"   CTTYPE 3  R 6  OMEGA 2.5  CD 5.5" +
                              "  I 1  SITECLASS \"E\"    Ss 1.5  S1 0.6  TL 12");
            }
            else
            {
                // Process all load definitions
                foreach (var loadDef in loadContainer.LoadDefinitions)
                {
                    // Format and write each load pattern
                    string loadPatternLine = FormatLoadPattern(loadDef);
                    sb.AppendLine(loadPatternLine);
                }

                // Add default seismic load patterns if they don't exist
                if (!loadContainer.LoadDefinitions.Any(ld =>
                    ld.Type?.ToLower() == "seismic" &&
                    ld.Name?.ToLower() == "eqx"))
                {
                    sb.AppendLine("  LOADPATTERN \"EQX\"  TYPE  \"Seismic\"  SELFWEIGHT  0");

                    // Default seismic parameters for X direction
                    string topStory = GetTopStoryName(loadContainer);
                    sb.AppendLine($"  SEISMIC \"EQX\"  \"ASCE 7-16\"    DIR \"X X+ECC X-ECC\"  ECC 0.05  TOPSTORY \"{topStory}\"" +
                                  "    BOTTOMSTORY \"Base\"   PERIODTYPE \"PROGCALC\"   CTTYPE 3  R 6  OMEGA 2.5  CD 5.5" +
                                  "  I 1  SITECLASS \"E\"    Ss 1.5  S1 0.6  TL 12");
                }

                if (!loadContainer.LoadDefinitions.Any(ld =>
                    ld.Type?.ToLower() == "seismic" &&
                    ld.Name?.ToLower() == "eqy"))
                {
                    sb.AppendLine("  LOADPATTERN \"EQY\"  TYPE  \"Seismic\"  SELFWEIGHT  0");

                    // Default seismic parameters for Y direction
                    string topStory = GetTopStoryName(loadContainer);
                    sb.AppendLine($"  SEISMIC \"EQY\"  \"ASCE 7-16\"    DIR \"Y Y+ECC Y-ECC\"  ECC 0.05  TOPSTORY \"{topStory}\"" +
                                  "    BOTTOMSTORY \"Base\"   PERIODTYPE \"PROGCALC\"   CTTYPE 3  R 6  OMEGA 2.5  CD 5.5" +
                                  "  I 1  SITECLASS \"E\"    Ss 1.5  S1 0.6  TL 12");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a single LoadDefinition as an E2K load pattern
        /// </summary>
        /// <param name="loadDef">LoadDefinition to format</param>
        /// <returns>E2K load pattern line</returns>
        private string FormatLoadPattern(LoadDefinition loadDef)
        {
            // Get standardized load type 
            string loadType = GetETABSLoadType(loadDef.Type);

            // Format: LOADPATTERN "SW"  TYPE  "Dead"  SELFWEIGHT  1
            return $"  LOADPATTERN \"{loadDef.Name}\"  TYPE  \"{loadType}\"  SELFWEIGHT  {loadDef.SelfWeight}";
        }

        /// <summary>
        /// Converts model load type to standard ETABS load type
        /// </summary>
        /// <param name="modelType">Load type from the model</param>
        /// <returns>Standardized ETABS load type</returns>
        private string GetETABSLoadType(string modelType)
        {
            if (string.IsNullOrEmpty(modelType))
                return "Dead"; // Default to dead load

            switch (modelType.ToLower())
            {
                case "dead":
                case "sw":
                case "selfweight":
                    return "Dead";

                case "live":
                case "ll":
                case "reducible live":
                    return "Live";

                case "sdl":
                case "superimposed dead":
                case "superimposed":
                    return "Dead";

                case "wind":
                case "wx":
                case "wy":
                    return "Wind";

                case "seismic":
                case "earthquake":
                case "eq":
                case "eqx":
                case "eqy":
                    return "Seismic";

                case "snow":
                    return "Snow";

                case "rain":
                case "rainwater":
                    return "Rain";

                case "temperature":
                case "temp":
                    return "Temperature";

                default:
                    // For any unrecognized type, categorize as Other
                    return "Other";
            }
        }

        // Gets the top story name from load definitions or defaults to "Story4"
       
        private string GetTopStoryName(LoadContainer loadContainer)
        {
            // Check if we have any surface loads with a level assignment
            // This would be more reliable in a complete implementation that has access to the model
            // For this implementation, we'll default to a standard story name
            return "Story4";
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Core.Models.Loads;

namespace ETABS.Import.Loads
{
    /// <summary>
    /// Converts Core LoadDefinition objects to ETABS E2K format text for Load Patterns section
    /// </summary>
    public class LoadPatternsImport
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
                    ld.Type == LoadType.Seismic &&
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
                    ld.Type == LoadType.Seismic &&
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

        // Formats a single LoadDefinition as an E2K load pattern
     
        private string FormatLoadPattern(LoadDefinition loadDef)
        {
            // Get standardized load type 
            string loadTypeStr = GetETABSLoadType(loadDef.Type);

            // Format: LOADPATTERN "SW"  TYPE  "Dead"  SELFWEIGHT  1
            return $"  LOADPATTERN \"{loadDef.Name}\"  TYPE  \"{loadTypeStr}\"  SELFWEIGHT  {loadDef.SelfWeight}";
        }

        /// <summary>
        /// Converts model load type to standard ETABS load type
        /// </summary>
        /// <param name="modelType">Load type from the model</param>
        /// <returns>Standardized ETABS load type</returns>
        private string GetETABSLoadType(LoadType modelType)
        {
            switch (modelType)
            {
                case LoadType.Dead:
                    return "Dead";
                case LoadType.Live:
                    return "Live";
                case LoadType.Wind:
                    return "Wind";
                case LoadType.Snow:
                    return "Snow";
                case LoadType.Seismic:
                    return "Seismic";
                case LoadType.Other:
                default:
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
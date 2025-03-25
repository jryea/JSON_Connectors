using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.ModelLayout;

namespace ETABS.Export.ModelLayout
{
    /// <summary>
    /// Converts Core Level objects to ETABS E2K format text
    /// </summary>
    public class LevelsExport
    {
        /// <summary>
        /// Converts a collection of Level objects to E2K format text
        /// </summary>
        /// <param name="levels">Collection of Level objects</param>
        /// <returns>E2K format text for levels</returns>
        public string ConvertToE2K(List<Level> levels)
        {
            StringBuilder sb = new StringBuilder();

            // Sort levels by elevation in descending order (top to bottom)
            var sortedLevels = new List<Level>(levels);
            sortedLevels.Sort((a, b) => b.ElevationOrHeight.CompareTo(a.ElevationOrHeight));

            // E2K Levels Section Header
            sb.AppendLine("$ STORIES - IN SEQUENCE FROM TOP");

            for (int i = 0; i < sortedLevels.Count - 1; i++)
            {
                var current = sortedLevels[i];
                var below = sortedLevels[i + 1];

                // Calculate story height
                double height = current.ElevationOrHeight - below.ElevationOrHeight;

                // Format: STORY "Story3" HEIGHT 120
                sb.AppendLine($"STORY \"{current.Name}\" HEIGHT {height}");
            }

            // Add base level with elevation
            var baseLevel = sortedLevels[sortedLevels.Count - 1];
            sb.AppendLine($"STORY \"{baseLevel.Name}\" ELEV {baseLevel.ElevationOrHeight}");

            return sb.ToString();
        }
    }
}
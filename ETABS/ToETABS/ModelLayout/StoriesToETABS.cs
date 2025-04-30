using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.ModelLayout;

namespace ETABS.ToETABS.ModelLayout
{
    // Converts Core Level objects to ETABS E2K format text
    public class StoriesToETABS
    {
        private List<string> _storyNames = new List<string>();

        // Converts a collection of Level objects to E2K format text
        public string ConvertToE2K(List<Level> levels)
        {
            // Sort levels by elevation in descending order (top to bottom)
            var sortedLevels = new List<Level>(levels);
            sortedLevels.Sort((a, b) => b.Elevation.CompareTo(a.Elevation));

            // Clear and populate _storyNames
            _storyNames.Clear();
            for (int i = 0; i < sortedLevels.Count; i++)
            {
                if (i == sortedLevels.Count - 1) // Lowest level
                {
                    _storyNames.Add("Base");
                }
                else
                {
                    _storyNames.Add($"Story{sortedLevels[i].Name}");
                }
            }

            StringBuilder sb = new StringBuilder();

            // E2K Levels Section Header
            sb.AppendLine("$ STORIES - IN SEQUENCE FROM TOP");

            for (int i = 0; i < sortedLevels.Count - 1; i++)
            {
                var current = sortedLevels[i];
                var below = sortedLevels[i + 1];

                // Calculate story height
                double height = current.Elevation - below.Elevation;

                // Format: STORY "Story3" HEIGHT 120
                sb.AppendLine($"\tSTORY \"Story{current.Name}\" HEIGHT {height}");
            }

            // Add base level with elevation
            var baseLevel = sortedLevels[sortedLevels.Count - 1];
            sb.AppendLine($"\tSTORY \"Base\" ELEV {baseLevel.Elevation}");

            return sb.ToString();
        }

        // Returns the list of valid story names
        public List<string> GetStoryNames()
        {
            return _storyNames;
        }
    }
}

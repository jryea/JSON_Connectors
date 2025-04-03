using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Models.ModelLayout;
using Core.Utilities;

namespace ETABS.Import.ModelLayout
{
    // Imports stories/levels from ETABS E2K file
    public class ETABSToStory
    {
        // Dictionary to map floor type names to IDs
        private Dictionary<string, string> _floorTypeIdsByName = new Dictionary<string, string>();

        // Sets the floor type name to ID mapping for reference when creating levels
        public void SetFloorTypes(Dictionary<string, string> floorTypeIdMapping)
        {
            _floorTypeIdsByName = new Dictionary<string, string>(floorTypeIdMapping);
        }

        // Imports stories/levels from E2K STORIES section
        public List<Level> Import(string storiesSection)
        {
            var levels = new List<Level>();

            if (string.IsNullOrWhiteSpace(storiesSection))
                return levels;

            // Regular expressions to match story definitions
            // Format: STORY "Story3" HEIGHT 120
            var storyHeightPattern = new Regex(@"^\s*STORY\s+""([^""]+)""\s+HEIGHT\s+([\d\.]+)",
                RegexOptions.Multiline);

            // Format: STORY "Base" ELEV 0
            var storyElevPattern = new Regex(@"^\s*STORY\s+""([^""]+)""\s+ELEV\s+([\d\.]+)",
                RegexOptions.Multiline);

            // First, parse base stories with direct elevation
            var elevMatches = storyElevPattern.Matches(storiesSection);
            var storyElevations = new Dictionary<string, double>();

            foreach (Match match in elevMatches)
            {
                if (match.Groups.Count >= 3)
                {
                    string storyName = match.Groups[1].Value;
                    double elevation = Convert.ToDouble(match.Groups[2].Value);

                    // Store elevation in dictionary
                    storyElevations[storyName] = elevation;

                    // Create level
                    var level = CreateLevel(storyName, elevation);
                    levels.Add(level);
                }
            }

            // Then, parse stories with heights and calculate elevations
            var heightMatches = storyHeightPattern.Matches(storiesSection);
            var storyHeights = new Dictionary<string, double>();

            foreach (Match match in heightMatches)
            {
                if (match.Groups.Count >= 3)
                {
                    string storyName = match.Groups[1].Value;
                    double height = Convert.ToDouble(match.Groups[2].Value);

                    // Store height in dictionary
                    storyHeights[storyName] = height;
                }
            }

            // Calculate elevations for stories defined by height
            // This requires a separate pass because we need to know the elevation of the story below
            CalculateStoriesElevation(storyHeights, storyElevations, levels);

            // Sort levels by elevation
            levels.Sort((a, b) => a.Elevation.CompareTo(b.Elevation));

            return levels;
        }

        // Creates a Level object with the given name and elevation
        private Level CreateLevel(string storyName, double elevation)
        {
            // Normalize story name
            string normalizedName = NormalizeStoryName(storyName);

            return new Level
            {
                Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                Name = normalizedName,
                Elevation = elevation,
                FloorTypeId = GetDefaultFloorTypeId()
            };
        }

        // Calculates elevations for stories defined by height
        private void CalculateStoriesElevation(
            Dictionary<string, double> storyHeights,
            Dictionary<string, double> storyElevations,
            List<Level> levels)
        {
            // Sort story names in ascending order (Base, Story1, Story2, etc.)
            var sortedStoryNames = new List<string>(storyHeights.Keys);
            sortedStoryNames.Sort((a, b) =>
            {
                // Special case for "Base" which should always be at the bottom
                if (a == "Base") return -1;
                if (b == "Base") return 1;

                // Extract numeric part and compare
                if (int.TryParse(ExtractNumericPart(a), out int aNum) &&
                    int.TryParse(ExtractNumericPart(b), out int bNum))
                {
                    return aNum.CompareTo(bNum);
                }

                // Fall back to string comparison
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            double currentElevation = 0;
            string prevStoryName = null;

            // Start with the base elevation if available
            if (storyElevations.TryGetValue("Base", out double baseElev))
            {
                currentElevation = baseElev;
                prevStoryName = "Base";
            }

            // Calculate elevations for each story
            foreach (string storyName in sortedStoryNames)
            {
                // Skip base story as it was already processed
                if (storyName == "Base") continue;

                // If elevation is already known, use it
                if (storyElevations.TryGetValue(storyName, out double elevation))
                {
                    currentElevation = elevation;
                    prevStoryName = storyName;
                    continue;
                }

                // Get story height
                if (storyHeights.TryGetValue(storyName, out double height) && prevStoryName != null)
                {
                    // Calculate elevation based on previous story elevation
                    currentElevation += height;

                    // Create level
                    var level = CreateLevel(storyName, currentElevation);
                    levels.Add(level);

                    prevStoryName = storyName;
                }
            }
        }

        // Normalizes a story name by removing "Story" prefix
        private string NormalizeStoryName(string storyName)
        {
            // Convert "Story1" to "1", "Base" stays as "Base"
            if (storyName.StartsWith("Story", StringComparison.OrdinalIgnoreCase))
            {
                return storyName.Substring(5);
            }

            return storyName;
        }

        // Extracts the numeric part from a story name
        private string ExtractNumericPart(string storyName)
        {
            // Extract numeric part from "Story1" or similar
            var match = Regex.Match(storyName, @"\d+");
            if (match.Success)
            {
                return match.Value;
            }

            return "0"; // Default value for non-numeric parts
        }

        // Gets the default floor type ID
        private string GetDefaultFloorTypeId()
        {
            // Return the first available floor type ID, or null if none available
            if (_floorTypeIdsByName.Count > 0)
            {
                return _floorTypeIdsByName.Values.First();
            }
            return null;
        }
    }
}
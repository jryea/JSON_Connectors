using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Models.ModelLayout;
using Core.Utilities;

namespace ETABS.Export.ModelLayout
{
    /// <summary>
    /// Imports stories/levels from ETABS E2K file
    /// </summary>
    public class StoryExport
    {
        // The floor type importer
        private readonly FloorTypeExport _floorTypeExporter = new FloorTypeExport();
        private Dictionary<string, string> _storyToFloorTypeMap = new Dictionary<string, string>();

        /// <summary>
        /// Imports stories/levels and floor types from E2K STORIES section
        /// </summary>
        public List<Level> Import(string storiesSection)
        {
            var levels = new List<Level>();

            if (string.IsNullOrWhiteSpace(storiesSection))
                return levels;

            if (_storyToFloorTypeMap.Count == 0)
            {
                _floorTypeExporter.Export(storiesSection);
                _storyToFloorTypeMap = _floorTypeExporter.GetFloorTypeMapping();    
            }

            // Get direct mapping from stories to floor type IDs
            var storyToFloorTypeMap = _floorTypeExporter.GetFloorTypeMapping();

            // Regular expressions to match story definitions
            var storyHeightPattern = new Regex(@"^\s*STORY\s+""([^""]+)""\s+HEIGHT\s+([\d\.]+)",
                RegexOptions.Multiline);

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

                    // Create level with the correct floor type ID
                    var level = new Level
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                        Name = NormalizeStoryName(storyName),
                        Elevation = elevation
                    };

                    // Assign floor type ID directly from storyToFloorTypeMap
                    if (_storyToFloorTypeMap.TryGetValue(storyName, out string floorTypeId))
                    {
                        level.FloorTypeId = floorTypeId;
                    }

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
                    storyHeights[storyName] = height;
                }
            }

            // Calculate elevations for stories defined by height
            CalculateStoriesElevation(storyHeights, storyElevations, levels, storyToFloorTypeMap);

            // Sort levels by elevation
            levels.Sort((a, b) => a.Elevation.CompareTo(b.Elevation));

            return levels;
        }

        // Calculates elevations for stories defined by height
        private void CalculateStoriesElevation(
            Dictionary<string, double> storyHeights,
            Dictionary<string, double> storyElevations,
            List<Level> levels,
            Dictionary<string, string> storyToFloorTypeMap)
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
                if (storyName == "Base") continue; // Skip base - already processed

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

                    // Create level with the correct floor type ID
                    var level = new Level
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                        Name = storyName,
                        Elevation = currentElevation
                    };

                    // Assign floor type ID directly from storyToFloorTypeMap
                    if (_storyToFloorTypeMap.TryGetValue(storyName, out string floorTypeId))
                    {
                        level.FloorTypeId = floorTypeId;
                    }

                    levels.Add(level);
                    prevStoryName = storyName;
                }
            }
        }

        // Normalizes a story name by removing "Story" prefix
        private string NormalizeStoryName(string storyName)
        {
            if (storyName.StartsWith("Story", StringComparison.OrdinalIgnoreCase))
                return storyName.Substring(5);

            return storyName;
        }

        // Helper methods
        private string ExtractNumericPart(string storyName)
        {
            var match = Regex.Match(storyName, @"\d+");
            return match.Success ? match.Value : "0";
        }

        // Method to set the floor type mapping from outside
        public void UseFloorTypeMapping(Dictionary<string, string> mapping)
        {
            _storyToFloorTypeMap = mapping ?? new Dictionary<string, string>();
        }
    }
}
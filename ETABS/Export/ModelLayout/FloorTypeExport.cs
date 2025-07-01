using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models.ModelLayout;
using Core.Utilities;

namespace ETABS.Export.ModelLayout
{
    // Imports floor type definitions from ETABS E2K file based on master stories
    public class FloorTypeExport
    {
        // Dictionary to map story names to floor type IDs
        private Dictionary<string, string> _floorTypesByStory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Imports floor types from E2K STORIES section
        public List<FloorType> Export(string storiesSection)
        {
            var floorTypes = new List<FloorType>();
            _floorTypesByStory.Clear();

            if (string.IsNullOrWhiteSpace(storiesSection))
                return floorTypes;

            // Patterns to match story definitions with MASTERSTORY or SIMILARTO
            var masterStoryPattern = new Regex(@"^\s*STORY\s+""([^""]+)""\s+.*\s+MASTERSTORY\s+""Yes""",
                RegexOptions.Multiline);

            var similarToPattern = new Regex(@"^\s*STORY\s+""([^""]+)""\s+.*\s+SIMILARTO\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Process master stories first
            var masterStoriesById = new Dictionary<string, FloorType>();
            var masterMatches = masterStoryPattern.Matches(storiesSection);

            foreach (Match match in masterMatches)
            {
                if (match.Groups.Count >= 2)
                {
                    string storyName = match.Groups[1].Value;

                    // Create a floor type for this master story
                    var floorType = new FloorType
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE),
                        Name = storyName,
                    };

                    floorTypes.Add(floorType);
                    masterStoriesById[storyName] = floorType;
                    _floorTypesByStory[storyName] = floorType.Id;
                }
            }

            // Process SIMILARTO stories
            var similarMatches = similarToPattern.Matches(storiesSection);
            foreach (Match match in similarMatches)
            {
                if (match.Groups.Count >= 3)
                {
                    string storyName = match.Groups[1].Value;
                    string masterStoryName = match.Groups[2].Value;

                    // Map to the master story's floor type if it exists
                    if (masterStoriesById.TryGetValue(masterStoryName, out FloorType masterFloorType))
                    {
                        _floorTypesByStory[storyName] = masterFloorType.Id;
                    }
                }
            }

            // Process all other stories
            var standardStoryPattern = new Regex(@"^\s*STORY\s+""([^""]+)""\s+",
                RegexOptions.Multiline);

            var standardMatches = standardStoryPattern.Matches(storiesSection);
            foreach (Match match in standardMatches)
            {
                string storyName = match.Groups[1].Value;

                // Skip if already processed as master or similar
                if (_floorTypesByStory.ContainsKey(storyName))
                    continue;

                // Skip story if it has MASTERSTORY or SIMILARTO (already processed)
                string fullLine = match.Value;
                if (fullLine.Contains("MASTERSTORY") || fullLine.Contains("SIMILARTO"))
                    continue;

                // Create a new floor type for this standard story
                var floorType = new FloorType
                {
                    Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE),
                    Name = NormalizeStoryName(storyName),
                };

                floorTypes.Add(floorType);
                _floorTypesByStory[storyName] = floorType.Id;
            }

            return floorTypes;
        }

        // Normalizes a story name by removing "Story" prefix
        private string NormalizeStoryName(string storyName)
        {
            if (storyName.StartsWith("Story", StringComparison.OrdinalIgnoreCase))
                return storyName.Substring(5);

            return storyName;
        }

        // Gets the floor type ID for a story
        public string GetFloorTypeIdForStory(string storyName)
        {
            if (_floorTypesByStory.TryGetValue(storyName, out string id))
                return id;

            return null;
        }

        // Gets the mapping from story names to floor type IDs
        public Dictionary<string, string> GetFloorTypeMapping()
        {
            return new Dictionary<string, string>(_floorTypesByStory);
        }
    }
}
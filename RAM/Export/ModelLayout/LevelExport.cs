using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.ModelLayout
{
    public class LevelExport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<int, string> _floorTypeMapping = new Dictionary<int, string>();

        public LevelExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public void SetFloorTypeMapping(Dictionary<int, string> floorTypeMapping)
        {
            _floorTypeMapping = floorTypeMapping ?? new Dictionary<int, string>();
        }

        public List<Level> Export()
        {
            var levels = new List<Level>();

            try
            {
                // Get stories from RAM
                IStories ramStories = _model.GetStories();
                if (ramStories == null || ramStories.GetCount() == 0)
                    return levels;

                // Default floor type ID to use if no mapping exists
                string defaultFloorTypeId = _floorTypeMapping.Values.FirstOrDefault();

                // Process each story
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory ramStory = ramStories.GetAt(i);
                    if (ramStory == null)
                        continue;

                    // Get floor type ID from mapping
                    string floorTypeId = defaultFloorTypeId;
                    if (_floorTypeMapping.TryGetValue(ramStory.GetFloorType().lUID, out string mappedFloorTypeId))
                    {
                        floorTypeId = mappedFloorTypeId;
                    }

                    // get elevation
                    double elevation = ramStory.dElevation;

                    // Create level
                    Level level = new Level
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                        Name = CleanStoryName(ramStory.strLabel),
                        FloorTypeId = floorTypeId,
                        Elevation = ConvertFromInches(elevation)
                    };

                    levels.Add(level);
                }

                // Add a level at elevation 0 if one doesn't already exist
                if (!levels.Any(l => Math.Abs(l.Elevation) < 0.001))
                {
                    // Get the Ground floor type ID
                    string groundFloorTypeId = null;

                    // Look for the Ground floor type in the mapping
                    if (_floorTypeMapping.TryGetValue(0, out groundFloorTypeId))
                    {
                        Console.WriteLine($"Found Ground floor type with ID: {groundFloorTypeId}");
                    }
                    else if (_floorTypeMapping.Values.Any())
                    {
                        // Use the first available floor type if Ground is not found
                        groundFloorTypeId = _floorTypeMapping.Values.First();
                        Console.WriteLine($"Using default floor type for level 0: {groundFloorTypeId}");
                    }

                    // Add a level at elevation 0
                    Level zeroLevel = new Level
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                        Name = "0",
                        FloorTypeId = groundFloorTypeId,
                        Elevation = 0.0
                    };

                    levels.Add(zeroLevel);
                    Console.WriteLine("Added level 0 at elevation 0");
                }

                // Sort levels by elevation for consistent ordering
                return levels.OrderBy(l => l.Elevation).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting levels from RAM: {ex.Message}");
                return levels;
            }
        }

        // Removes "Story" prefix if present to normalize names
        private string CleanStoryName(string storyName)
        {
            if (storyName.StartsWith("Story ", StringComparison.OrdinalIgnoreCase))
            {
                return storyName.Substring(6).Trim();
            }
            else if (storyName.StartsWith("Story", StringComparison.OrdinalIgnoreCase))
            {
                return storyName.Substring(5).Trim();
            }
            return storyName;
        }

        private double ConvertFromInches(double inches)
        {
            switch (_lengthUnit.ToLower())
            {
                case "feet":
                    return inches / 12.0;
                case "millimeters":
                    return inches * 25.4;
                case "centimeters":
                    return inches * 2.54;
                case "meters":
                    return inches * 0.0254;
                case "inches":
                default:
                    return inches;
            }
        }

        // Helper method to create a mapping from level IDs to RAM story UIDs
        public Dictionary<string, int> CreateLevelMapping(List<Level> levels)
        {
            var mapping = new Dictionary<string, int>();

            try
            {
                // Get stories from RAM
                IStories ramStories = _model.GetStories();
                if (ramStories == null || ramStories.GetCount() == 0 || levels == null || levels.Count == 0)
                    return mapping;

                // Create a lookup by name for quick access
                Dictionary<string, string> levelIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Name))
                    {
                        levelIdsByName[level.Name] = level.Id;
                        // Also add with "Story" prefix for flexibility
                        levelIdsByName[$"Story{level.Name}"] = level.Id;
                        levelIdsByName[$"Story {level.Name}"] = level.Id;
                    }
                }

                // Map RAM stories to Core model level IDs
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory ramStory = ramStories.GetAt(i);
                    if (ramStory != null && !string.IsNullOrEmpty(ramStory.strLabel))
                    {
                        if (levelIdsByName.TryGetValue(ramStory.strLabel, out string levelId))
                        {
                            mapping[levelId] = ramStory.lUID;
                        }
                    }
                }

                // If we have a Foundation level at elevation 0, map it appropriately
                string foundationLevelId = levels.FirstOrDefault(l => Math.Abs(l.Elevation) < 0.001)?.Id;
                if (!string.IsNullOrEmpty(foundationLevelId) && !mapping.ContainsKey(foundationLevelId))
                {
                    // Use the lowest story UID as a fallback for foundation level
                    int lowestStoryUid = ramStories.GetCount() > 0 ? ramStories.GetAt(0).lUID : 1;
                    mapping[foundationLevelId] = lowestStoryUid;
                }

                return mapping;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating level mapping: {ex.Message}");
                return mapping;
            }
        }
    }
}
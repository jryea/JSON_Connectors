using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private string _groundFloorTypeId;

        public LevelExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
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
                    if (ramStory.GetFloorType() != null)
                    {
                        int ramFloorTypeUid = ramStory.GetFloorType().lUID;
                        if (_floorTypeMapping.TryGetValue(ramFloorTypeUid, out string mappedFloorTypeId))
                        {
                            floorTypeId = mappedFloorTypeId;
                        }
                    }

                    // Get elevation
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
                bool hasLevelZero = levels.Any(l => Math.Abs(l.Elevation) < 0.001);
                if (!hasLevelZero)
                {
                    // Add a level at elevation 0 with temporary FloorTypeId
                    Level zeroLevel = new Level
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                        Name = "0",
                        FloorTypeId = defaultFloorTypeId, // Temporary - will update below
                        Elevation = 0.0
                    };

                    levels.Add(zeroLevel);
                    Console.WriteLine("Added level 0 at elevation 0");
                    hasLevelZero = true;
                }

                // Get the Ground floor type ID - this is the key change
                // It should be passed in during SetFloorTypeMapping
                string groundFloorTypeId = _groundFloorTypeId;

                // If ground floor type ID is available, update all level 0 objects
                if (!string.IsNullOrEmpty(groundFloorTypeId) && hasLevelZero)
                {
                    foreach (var level in levels.Where(l => Math.Abs(l.Elevation) < 0.001))
                    {
                        level.FloorTypeId = groundFloorTypeId;
                        Console.WriteLine($"Set Level 0 FloorTypeId to Ground floor type ID: {groundFloorTypeId}");
                    }
                }
                else
                {
                    Console.WriteLine("Warning: No Ground floor type ID available for Level 0");
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

        public void SetFloorTypeMapping(Dictionary<int, string> floorTypeMapping, string groundFloorTypeId = null)
        {
            _floorTypeMapping = floorTypeMapping ?? new Dictionary<int, string>();
            _groundFloorTypeId = groundFloorTypeId;

            if (!string.IsNullOrEmpty(groundFloorTypeId))
            {
                Console.WriteLine($"Ground floor type ID set: {groundFloorTypeId}");
            }
            else
            {
                Console.WriteLine("Warning: No Ground floor type ID provided");
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
        public Dictionary<string, string> CreateLevelMapping(List<Level> levels)
        {
            var mapping = new Dictionary<string, string>();

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
                            mapping[levelId] = ramStory.lUID.ToString();
                        }
                    }
                }

                // If we have a Foundation level at elevation 0, map it appropriately
                string foundationLevelId = levels.FirstOrDefault(l => Math.Abs(l.Elevation) < 0.001)?.Id;
                if (!string.IsNullOrEmpty(foundationLevelId) && !mapping.ContainsKey(foundationLevelId))
                {
                    // Use the lowest story UID as a fallback for foundation level
                    int lowestStoryUid = ramStories.GetCount() > 0 ? ramStories.GetAt(0).lUID : 1;
                    mapping[foundationLevelId] = lowestStoryUid.ToString(); ;
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
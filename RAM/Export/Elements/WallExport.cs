using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Elements
{
    public class WallExport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, string> _levelIdToNameMapping = new Dictionary<string, string>();
        private Dictionary<string, string> _nameToLevelIdMapping = new Dictionary<string, string>();
        private Dictionary<string, string> _wallPropMappings = new Dictionary<string, string>();

        public WallExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public void SetLevelMappings(Dictionary<string, string> levelMappings)
        {
            _levelIdToNameMapping = new Dictionary<string, string>();
            _nameToLevelIdMapping = new Dictionary<string, string>();

            foreach (var kvp in levelMappings)
            {
                _levelIdToNameMapping[kvp.Key] = kvp.Value;
                _nameToLevelIdMapping[kvp.Value] = kvp.Key;
            }
        }

        public void SetWallPropertyMappings(Dictionary<string, string> wallPropMappings)
        {
            _wallPropMappings = wallPropMappings ?? new Dictionary<string, string>();
        }

        public List<Wall> Export()
        {
            var walls = new List<Wall>();

            try
            {
                // Get all stories from RAM
                IStories ramStories = _model.GetStories();
                if (ramStories == null || ramStories.GetCount() == 0)
                    return walls;

                // Process each story
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory ramStory = ramStories.GetAt(i);
                    if (ramStory == null)
                        continue;

                    // Find the corresponding level ID for this story
                    string storyName = ramStory.strLabel;
                    string levelId = FindLevelIdByStoryName(storyName);

                    if (string.IsNullOrEmpty(levelId))
                        continue;

                    // Get walls for this story
                    IWalls storyWalls = ramStory.GetWalls();
                    if (storyWalls == null || storyWalls.GetCount() == 0)
                        continue;

                    // Process each wall in the story
                    for (int j = 0; j < storyWalls.GetCount(); j++)
                    {
                        IWall ramWall = storyWalls.GetAt(j);
                        if (ramWall == null)
                            continue;

                        // Get wall coordinates
                        SCoordinate baseStartPt = new SCoordinate();
                        SCoordinate baseEndPt = new SCoordinate();
                        SCoordinate topStartPt = new SCoordinate();
                        SCoordinate topEndPt = new SCoordinate();
                        ramWall.GetEndCoordinates(ref topStartPt, ref topEndPt, ref baseStartPt, ref baseEndPt);

                        // Create points list for the wall
                        List<Point2D> points = new List<Point2D>
                        {
                            new Point2D(
                                ConvertFromInches(topStartPt.dXLoc),
                                ConvertFromInches(topStartPt.dYLoc)
                            ),
                            new Point2D(
                                ConvertFromInches(topEndPt.dXLoc),
                                ConvertFromInches(topEndPt.dYLoc)
                            )
                        };

                        // Find base and top level IDs
                        string baseLevelId = FindBaseLevelIdForStory(ramStory);
                        string topLevelId = levelId;

                        // Create wall from RAM data
                        Wall wall = new Wall
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.WALL),
                            Points = points,
                            BaseLevelId = baseLevelId,
                            TopLevelId = topLevelId,
                            PropertiesId = FindWallPropertiesId(ramWall)
                        };

                        walls.Add(wall);
                    }
                }

                return walls;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting walls from RAM: {ex.Message}");
                return walls;
            }
        }

        private string FindLevelIdByStoryName(string storyName)
        {
            string levelId = null;

            // Try direct mapping first
            if (_nameToLevelIdMapping.TryGetValue(storyName, out levelId))
                return levelId;

            // Try with "Story" prefix removed
            string cleanName = CleanStoryName(storyName);
            if (_nameToLevelIdMapping.TryGetValue(cleanName, out levelId))
                return levelId;

            // Try with "Story" prefix variations
            if (_nameToLevelIdMapping.TryGetValue($"Story {cleanName}", out levelId) ||
                _nameToLevelIdMapping.TryGetValue($"Story{cleanName}", out levelId))
                return levelId;

            // Return null if no mapping found
            return null;
        }

        private string FindBaseLevelIdForStory(IStory story)
        {
            if (story == null)
                return null;

            // Try to find the level below this story
            IStories ramStories = _model.GetStories();
            IStory belowStory = null;
            double maxElevation = double.MinValue;

            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory checkStory = ramStories.GetAt(i);
                if (checkStory != null && checkStory.dElevation < story.dElevation &&
                    checkStory.dElevation > maxElevation)
                {
                    belowStory = checkStory;
                    maxElevation = checkStory.dElevation;
                }
            }

            if (belowStory != null)
            {
                return FindLevelIdByStoryName(belowStory.strLabel);
            }

            // If no level below found, look for level with elevation 0
            foreach (var entry in _nameToLevelIdMapping)
            {
                if (entry.Key.Equals("0", StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            // If still not found, use the same level ID as top level
            return FindLevelIdByStoryName(story.strLabel);
        }

        private string FindWallPropertiesId(IWall wall)
        {
            if (wall == null)
                return _wallPropMappings.Values.FirstOrDefault();

            // Try to find wall property by thickness
            double thickness = wall.dThickness;

            // Look for a wall property with matching thickness
            foreach (var entry in _wallPropMappings)
            {
                // This is a simplified approach - in a real implementation,
                // you would need to retrieve the actual wall properties and compare
                if (entry.Key.Contains(thickness.ToString("0.##")))
                    return entry.Value;
            }

            // Return first wall property ID as fallback
            return _wallPropMappings.Values.FirstOrDefault();
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
    }
}
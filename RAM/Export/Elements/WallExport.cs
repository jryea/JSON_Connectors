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
        private Dictionary<string, string> _levelMappings = new Dictionary<string, string>();
        private Dictionary<string, string> _wallPropMappings = new Dictionary<string, string>();

        public WallExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public void SetLevelMappings(Dictionary<string, string> levelMappings)
        {
            _levelMappings = levelMappings ?? new Dictionary<string, string>();
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
                    string levelId = Helpers.FindLevelIdForStory(ramStory, _levelMappings);
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
                        string baseLevelId = Helpers.FindBaseLevelIdForStory(ramStory, _model, _levelMappings);
                        string topLevelId = FindTopLevelIdForWall(ramWall, ramStory);

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

        private string FindTopLevelIdForWall(IWall wall, IStory currentStory)
        {
            string currentStoryName = currentStory.strLabel;
            if (_levelMappings.TryGetValue(currentStoryName, out string levelId))
                return levelId;

            // Try with "Story" prefix variations
            if (_levelMappings.TryGetValue($"Story {currentStoryName}", out levelId) ||
                _levelMappings.TryGetValue($"Story{currentStoryName}", out levelId))
                return levelId;

            // Return first level ID as fallback
            return _levelMappings.Values.FirstOrDefault();
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
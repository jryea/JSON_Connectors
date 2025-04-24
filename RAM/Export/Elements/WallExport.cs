using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Elements
{
    public class WallExport
    {
        private IModel _model;
        private string _lengthUnit;

        public WallExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
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

                    // Find the corresponding level ID for this story using the mapping utility
                    string storyUid = ramStory.lUID.ToString();
                    string topLevelId = ModelMappingUtility.GetLevelIdForStoryUid(storyUid);

                    if (string.IsNullOrEmpty(topLevelId))
                    {
                        Console.WriteLine($"No level mapping found for story {ramStory.strLabel} (UID: {storyUid})");
                        continue;
                    }

                    // Find base level ID for this story
                    string baseLevelId = ModelMappingUtility.GetBaseLevelIdForTopLevelId(topLevelId, _model);

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

                        // Create points list for the wall, converting units as needed
                        List<Point2D> points = new List<Point2D>
                        {
                            new Point2D(
                                UnitConversionUtils.ConvertFromInches(topStartPt.dXLoc, _lengthUnit),
                                UnitConversionUtils.ConvertFromInches(topStartPt.dYLoc, _lengthUnit)
                            ),
                            new Point2D(
                                UnitConversionUtils.ConvertFromInches(topEndPt.dXLoc, _lengthUnit),
                                UnitConversionUtils.ConvertFromInches(topEndPt.dYLoc, _lengthUnit)
                            )
                        };

                        // Get the wall thickness and find the matching property
                        double thickness = UnitConversionUtils.ConvertFromInches(ramWall.dThickness, _lengthUnit);
                        thickness = Math.Round(thickness, 2); // Round to avoid floating point issues

                        // Find the matching wall property ID from the pre-created properties
                        string propertiesId = ModelMappingUtility.GetWallPropertyIdForThickness(thickness);

                        // Create wall from RAM data with reference to pre-created properties
                        Wall wall = new Wall
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.WALL),
                            Points = points,
                            BaseLevelId = baseLevelId,
                            TopLevelId = topLevelId,
                            PropertiesId = propertiesId
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
    }
}
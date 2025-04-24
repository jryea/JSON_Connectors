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
    public class FloorExport
    {
        private IModel _model;
        private string _lengthUnit;

        public FloorExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public List<Floor> Export()
        {
            var floors = new List<Floor>();

            try
            {
                IFloorTypes floorTypes = _model.GetFloorTypes();
                if (floorTypes == null || floorTypes.GetCount() == 0)
                    return floors;

                // Process each floor type
                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType floorType = floorTypes.GetAt(i);
                    if (floorType == null)
                        continue;

                    // Find floor property ID (if available)
                    string floorPropertiesId = ModelMappingUtility.GetFloorTypeIdForUid(floorType.lUID.ToString());

                    // Get slab perimeters for this floor type
                    ISlabPerimeters slabPerimeters = floorType.GetSlabPerimeters();
                    if (slabPerimeters == null || slabPerimeters.GetCount() == 0)
                        continue;

                    // Process each story that uses this floor type
                    IStories ramStories = _model.GetStories();
                    if (ramStories == null || ramStories.GetCount() == 0)
                        continue;

                    for (int k = 0; k < ramStories.GetCount(); k++)
                    {
                        IStory ramStory = ramStories.GetAt(k);
                        if (ramStory == null || ramStory.GetFloorType() == null ||
                            ramStory.GetFloorType().lUID != floorType.lUID)
                            continue;

                        // Find the corresponding level ID for this story
                        string levelId = ModelMappingUtility.GetLevelIdForStoryUid(ramStory.lUID.ToString());
                        if (string.IsNullOrEmpty(levelId))
                        {
                            Console.WriteLine($"No level mapping found for story {ramStory.strLabel} (UID: {ramStory.lUID})");
                            continue;
                        }

                        // Process each slab perimeter
                        for (int j = 0; j < slabPerimeters.GetCount(); j++)
                        {
                            ISlabPerimeter slabPerimeter = slabPerimeters.GetAt(j);
                            if (slabPerimeter == null)
                                continue;

                            List<Point2D> floorPoints = new List<Point2D>();

                            // Get points for the slab perimeter
                            IPoints slabPerimeterPoints = slabPerimeter.GetPerimeterVertices();
                            if (slabPerimeterPoints == null || slabPerimeterPoints.GetCount() < 3)
                                continue;

                            // Extract points from the perimeter
                            for (int p = 0; p < slabPerimeterPoints.GetCount(); p++)
                            {
                                IPoint slabPerimeterPoint = slabPerimeterPoints.GetAt(p);
                                if (slabPerimeterPoint == null)
                                    continue;

                                // Get the coordinates of the slab perimeter point
                                SCoordinate slabPoint = new SCoordinate();
                                slabPerimeterPoint.GetCoordinate(ref slabPoint);

                                // Convert to Point2D
                                Point2D point2D = new Point2D(
                                    UnitConversionUtils.ConvertFromInches(slabPoint.dXLoc, "inches"),
                                    UnitConversionUtils.ConvertFromInches(slabPoint.dYLoc, "inches")
                                );

                                floorPoints.Add(point2D);
                            }

                            // Skip floors with insufficient points
                            if (floorPoints.Count < 3)
                                continue;

                            // Create floor from RAM data
                            Floor floor = new Floor
                            {
                                Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR),
                                LevelId = levelId,
                                FloorPropertiesId = floorPropertiesId,
                                Points = floorPoints,
                                DiaphragmId = null, // Assuming no diaphragm mapping for now
                                SurfaceLoadId = null // Assuming no surface load mapping for now
                            };

                            floors.Add(floor);
                        }
                    }
                }

                return floors;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting floors from RAM: {ex.Message}");
                return floors;
            }
        }
    }
}
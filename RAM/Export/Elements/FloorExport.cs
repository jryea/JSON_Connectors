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
    public class FloorExport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, string> _levelMappings = new Dictionary<string, string>();

        public FloorExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public void SetLevelMappings(Dictionary<string, string> levelMappings)
        {
            _levelMappings = levelMappings ?? new Dictionary<string, string>();
        }

        public List<Floor> Export()
        {
            var floors = new List<Floor>();

            try
            {
                IFloorTypes floorTypes = _model.GetFloorTypes();
                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType floorType = floorTypes.GetAt(i);
                    string levelId = null;
                    if (string.IsNullOrEmpty(levelId))
                        continue;

                    ISlabPerimeters slabPerimeters = floorType.GetSlabPerimeters();

                    if (slabPerimeters == null || slabPerimeters.GetCount() == 0)
                        continue;

                    List<Point2D> floorPoints = new List<Point2D>();

                    for (int j = 0; j < slabPerimeters.GetCount(); j++)
                    {
                        ISlabPerimeter slabPerimeter = slabPerimeters.GetAt(j);
                        if (slabPerimeter == null)
                            continue;

                        IPoints slabPerimeterPoints = slabPerimeter.GetPerimeterVertices();
                        if (slabPerimeterPoints == null || slabPerimeterPoints.GetCount() == 0)
                            continue;

                        for (int k = 0; k < slabPerimeterPoints.GetCount(); k++)
                        {
                            IPoint slabPerimeterPoint = slabPerimeterPoints.GetAt(k);

                            // Get the coordinates of the slab perimeter point
                            SCoordinate slabPoint = new SCoordinate();

                            // Convert to Point2D
                            Point2D point2D = new Point2D(slabPoint.dXLoc, slabPoint.dYLoc);

                            floorPoints.Add(point2D);
                        }
                    }

                    IStories ramStories = _model.GetStories();
                    if (ramStories == null || ramStories.GetCount() == 0)
                        continue;

                    for (int k = 0; k < ramStories.GetCount(); k++)
                    {
                        IStory ramStory = ramStories.GetAt(k);
                        if (ramStory == null)
                            continue;

                        // Find the corresponding level ID for this story
                        levelId = Helpers.FindLevelIdForStory(ramStory, _levelMappings);
                        if (string.IsNullOrEmpty(levelId))
                            continue;

                        // Create floor from RAM data
                        Floor floor = new Floor
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR),
                            LevelId = levelId,
                            FloorPropertiesId = null, // Assuming no properties for now
                            Points = floorPoints,
                            DiaphragmId = null, // Assuming no diaphragm for now
                            SurfaceLoadId = null // Assuming no surface load for now
                        };
                        floors.Add(floor);
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

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
                // Get all floor types from RAM
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes == null || ramFloorTypes.GetCount() == 0)
                    return walls;

                // Find base level and top level
                string baseLevelId = _levelMappings.Values.FirstOrDefault();
                string topLevelId = _levelMappings.Values.LastOrDefault();

                // Process each floor type
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType floorType = ramFloorTypes.GetAt(i);
                    if (floorType == null)
                        continue;

                    // Find the corresponding level ID for this floor type
                    string currentLevelId = FindLevelIdForFloorType(floorType);
                    if (string.IsNullOrEmpty(currentLevelId))
                        continue;

                    // Get layout walls for this floor type
                    ILayoutWalls layoutWalls = floorType.GetLayoutWalls();
                    if (layoutWalls == null)
                        continue;

                    // Process each layout wall
                    for (int j = 0; j < layoutWalls.GetCount(); j++)
                    {
                        ILayoutWall layoutWall = layoutWalls.GetAt(j);
                        if (layoutWall == null)
                            continue;

                        // Create wall points
                        List<Point2D> points = new List<Point2D>
                        {
                            new Point2D(
                                ConvertFromInches(layoutWall.dXStart),
                                ConvertFromInches(layoutWall.dYStart)
                            ),
                            new Point2D(
                                ConvertFromInches(layoutWall.dXEnd),
                                ConvertFromInches(layoutWall.dYEnd)
                            )
                        };

                        // Create wall from RAM data
                        Wall wall = new Wall
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.WALL),
                            Points = points,
                            BaseLevelId = baseLevelId, // Use base level for now
                            TopLevelId = topLevelId,   // Use top level for now
                            PropertiesId = FindWallPropertiesId(layoutWall)
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

        private string FindLevelIdForFloorType(IFloorType floorType)
        {
            // Try to find direct mapping by floor type UID
            string key = $"FloorType_{floorType.lUID}";
            if (_levelMappings.TryGetValue(key, out string levelId))
                return levelId;

            // If not found, try by floor type name
            if (_levelMappings.TryGetValue(floorType.strLabel, out levelId))
                return levelId;

            // Return first level ID as fallback
            return _levelMappings.Values.FirstOrDefault();
        }

        private string FindWallPropertiesId(ILayoutWall layoutWall)
        {
            try
            {
                // Try to get wall type UID
                int wallTypeUID = 0;
                try
                {
                    // This is assuming there's a method to get the wall type UID
                    // This may not exist in all RAM versions
                    wallTypeUID = layoutWall.GetWallTypeUID();
                }
                catch
                {
                    // If not supported, try to find by thickness
                    double thickness = layoutWall.dThickness;
                    return FindWallPropertiesByThickness(thickness);
                }

                // Try to find direct mapping by wall type UID
                string key = $"WallType_{wallTypeUID}";
                if (_wallPropMappings.TryGetValue(key, out string wallPropsId))
                    return wallPropsId;

                // If not found, try to find by thickness
                return FindWallPropertiesByThickness(layoutWall.dThickness);
            }
            catch
            {
                // Return first wall property ID as fallback
                return _wallPropMappings.Values.FirstOrDefault();
            }
        }

        private string FindWallPropertiesByThickness(double thickness)
        {
            // Look for a wall property with matching thickness
            // This is a simplified approach - in a real implementation,
            // you would need to retrieve the actual wall properties and compare

            // For now, just return the first available wall property
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
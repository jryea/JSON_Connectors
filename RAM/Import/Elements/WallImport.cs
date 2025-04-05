// WallImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    // Imports wall elements to RAM from the Core model
    public class WallImport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, IFloorType> _floorTypeMap = new Dictionary<string, IFloorType>();

        public WallImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
            InitializeFloorTypeMap();
        }

        // Initializes the floor type mapping
        private void InitializeFloorTypeMap()
        {
            try
            {
                _floorTypeMap.Clear();
                IFloorTypes floorTypes = _model.GetFloorTypes();

                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType floorType = floorTypes.GetAt(i);
                    _floorTypeMap[floorType.strLabel] = floorType;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing floor type map: {ex.Message}");
            }
        }

        // Imports walls to RAM model for each floor type
       
        public int Import(IEnumerable<Wall> walls, IEnumerable<Level> levels, IEnumerable<WallProperties> wallProperties)
        {
            try
            {
                int count = 0;
                Dictionary<string, Level> levelMap = new Dictionary<string, Level>();
                Dictionary<string, double> thicknessMap = new Dictionary<string, double>();

                // Build level mapping
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id))
                    {
                        levelMap[level.Id] = level;
                    }
                }

                // Build wall properties (thickness) mapping
                foreach (var wallProp in wallProperties)
                {
                    if (!string.IsNullOrEmpty(wallProp.Id))
                    {
                        thicknessMap[wallProp.Id] = wallProp.Thickness;
                    }
                }

                // Process all walls
                foreach (var wall in walls)
                {
                    if (wall.Points == null || wall.Points.Count < 2 ||
                        string.IsNullOrEmpty(wall.BaseLevelId) ||
                        string.IsNullOrEmpty(wall.PropertiesId))
                    {
                        continue;
                    }

                    // Get the level and floor type
                    if (!levelMap.TryGetValue(wall.BaseLevelId, out Level baseLevel) ||
                        baseLevel.FloorTypeId == null ||
                        !GetRamFloorTypeByFloorTypeId(baseLevel.FloorTypeId, out IFloorType floorType))
                    {
                        continue;
                    }

                    // Get wall thickness
                    double thickness = 8.0; // Default 8 inches if not specified
                    if (thicknessMap.TryGetValue(wall.PropertiesId, out double wallThickness))
                    {
                        thickness = Helpers.ConvertToInches(wallThickness, _lengthUnit);
                    }

                    // In RAM, walls are defined by start and end points
                    // For our model, we need to convert the wall points to a line segment
                    // We'll use the first two points to create the wall, simplifying the process

                    // Convert coordinates to inches
                    double startX = Helpers.ConvertToInches(wall.Points[0].X, _lengthUnit);
                    double startY = Helpers.ConvertToInches(wall.Points[0].Y, _lengthUnit);
                    double endX = Helpers.ConvertToInches(wall.Points[1].X, _lengthUnit);
                    double endY = Helpers.ConvertToInches(wall.Points[1].Y, _lengthUnit);

                    // Create the wall in RAM
                    // RAM walls need start and end points with Z coordinates for base and top
                    // For simplicity, we'll use 0 for all Z coordinates
                    ILayoutWalls layoutWalls = floorType.GetLayoutWalls();
                    ILayoutWall ramWall = layoutWalls.Add(EMATERIALTYPES.EConcreteMat,
                                                          startX, startY, 0.0, 0.0,
                                                          endX, endY, 0.0, 0.0,
                                                          thickness);

                    if (ramWall != null)
                    {
                        count++;
                    }

                    // If the wall has more than 2 points, we'll create additional walls for each segment
                    for (int i = 1; i < wall.Points.Count - 1; i++)
                    {
                        startX = Helpers.ConvertToInches(wall.Points[i].X, _lengthUnit);
                        startY = Helpers.ConvertToInches(wall.Points[i].Y, _lengthUnit);
                        endX = Helpers.ConvertToInches(wall.Points[i + 1].X, _lengthUnit);
                        endY = Helpers.ConvertToInches(wall.Points[i + 1].Y, _lengthUnit);

                        ramWall = layoutWalls.Add(EMATERIALTYPES.EConcreteMat,
                                                startX, startY, 0.0, 0.0,
                                                endX, endY, 0.0, 0.0,
                                                thickness);

                        if (ramWall != null)
                        {
                            count++;
                        }
                    }

                    // If the wall is defined as a closed loop (polygon), connect the last point to the first
                    if (wall.Points.Count > 2)
                    {
                        int lastIndex = wall.Points.Count - 1;
                        startX = Helpers.ConvertToInches(wall.Points[lastIndex].X, _lengthUnit);
                        startY = Helpers.ConvertToInches(wall.Points[lastIndex].Y, _lengthUnit);
                        endX = Helpers.ConvertToInches(wall.Points[0].X, _lengthUnit);
                        endY = Helpers.ConvertToInches(wall.Points[0].Y, _lengthUnit);

                        // Check if this is actually a new segment (not the same as the first point)
                        if (startX != endX || startY != endY)
                        {
                            ramWall = layoutWalls.Add(EMATERIALTYPES.EConcreteMat,
                                                    startX, startY, 0.0, 0.0,
                                                    endX, endY, 0.0, 0.0,
                                                    thickness);

                            if (ramWall != null)
                            {
                                count++;
                            }
                        }
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing walls: {ex.Message}");
                throw;
            }
        }

        // Gets a RAM floor type by floor type ID from our model
        private bool GetRamFloorTypeByFloorTypeId(string floorTypeId, out IFloorType floorType)
        {
            floorType = null;

            try
            {
                // Try to find a floor type with a matching ID
                // In a real implementation, we would have a mapping from our model's floor type IDs
                // to RAM floor type names, but for simplicity, we'll just use the first floor type

                if (_floorTypeMap.Count > 0)
                {
                    floorType = _floorTypeMap.Values.GetEnumerator().Current;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
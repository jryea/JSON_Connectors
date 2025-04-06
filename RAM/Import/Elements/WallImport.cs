// WallImport.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
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
        private Dictionary<string, IFloorType> _floorTypeByLevelId = new Dictionary<string, IFloorType>();

        // Initializes a new instance of the WallImport class
      
        public WallImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        // Imports walls to RAM model
       
        public int Import(IEnumerable<Wall> walls, IEnumerable<Level> levels, IEnumerable<WallProperties> wallProperties)
        {
            try
            {
                // First, ensure we have valid input
                if (walls == null || !walls.Any())
                {
                    Console.WriteLine("No walls to import.");
                    return 0;
                }

                if (levels == null || !levels.Any())
                {
                    Console.WriteLine("No levels available for wall import.");
                    return 0;
                }

                // Get all available floor types from RAM
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                {
                    Console.WriteLine("Error: No floor types found in RAM model");
                    return 0;
                }

                // Use the first floor type as default
                IFloorType defaultFloorType = ramFloorTypes.GetAt(0);
                Console.WriteLine($"Using default floor type: {defaultFloorType.strLabel} (ID: {defaultFloorType.lUID})");

                // Create mappings for levels and properties
                Dictionary<string, Level> levelMap = new Dictionary<string, Level>();
                Dictionary<string, WallProperties> wallPropsMap = new Dictionary<string, WallProperties>();

                // Build level mapping
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id))
                    {
                        levelMap[level.Id] = level;

                        // Setup floor type mapping for each level
                        if (string.IsNullOrEmpty(level.FloorTypeId))
                        {
                            _floorTypeByLevelId[level.Id] = defaultFloorType;
                        }
                        else
                        {
                            // In a real implementation, match by level's floor type ID
                            // For now, just use the first floor type
                            _floorTypeByLevelId[level.Id] = ramFloorTypes.GetAt(0);
                        }
                    }
                }

                // Build wall properties mapping
                foreach (var wallProp in wallProperties)
                {
                    if (!string.IsNullOrEmpty(wallProp.Id))
                    {
                        wallPropsMap[wallProp.Id] = wallProp;
                    }
                }

                // Now process and import the walls
                int count = 0;
                foreach (var wall in walls)
                {
                    if (wall.Points == null || wall.Points.Count < 2)
                    {
                        Console.WriteLine($"Skipping wall {wall.Id}: Insufficient points defined");
                        continue;
                    }

                    // Get the floor type for this wall
                    IFloorType floorType = null;

                    if (!string.IsNullOrEmpty(wall.BaseLevelId) && _floorTypeByLevelId.TryGetValue(wall.BaseLevelId, out floorType))
                    {
                        // We found the floor type directly
                    }
                    else if (_floorTypeByLevelId.Count > 0)
                    {
                        // Use the first available floor type if no specific mapping
                        floorType = _floorTypeByLevelId.Values.First();
                    }
                    else
                    {
                        floorType = defaultFloorType;
                    }

                    // Get wall thickness
                    double thickness = 8.0; // Default thickness in inches
                    if (!string.IsNullOrEmpty(wall.PropertiesId) && wallPropsMap.TryGetValue(wall.PropertiesId, out WallProperties wallProp))
                    {
                        thickness = Helpers.ConvertToInches(wallProp.Thickness, _lengthUnit);
                    }

                    // For RAM walls, we need start and end points
                    // We'll use the first two points in the wall's point collection
                    Point2D startPoint = wall.Points[0];
                    Point2D endPoint = wall.Points[1];

                    // Convert coordinates to inches
                    double startX = Helpers.ConvertToInches(startPoint.X, _lengthUnit);
                    double startY = Helpers.ConvertToInches(startPoint.Y, _lengthUnit);
                    double endX = Helpers.ConvertToInches(endPoint.X, _lengthUnit);
                    double endY = Helpers.ConvertToInches(endPoint.Y, _lengthUnit);
                    double startZ = 0;  // Z is determined by level
                    double endZ = 0;    // Z is determined by level

                    try
                    {
                        // Create the wall in RAM
                        ILayoutWalls layoutWalls = floorType.GetLayoutWalls();
                        if (layoutWalls == null)
                        {
                            Console.WriteLine($"Error: GetLayoutWalls() returned null for floor type {floorType.strLabel}");
                            continue;
                        }

                        // RAM uses concrete material type for walls
                        ILayoutWall ramWall = layoutWalls.Add(
                            EMATERIALTYPES.EConcreteMat,
                            startX, startY, startZ, 0,
                            endX, endY, endZ, 0,
                            thickness);

                        if (ramWall != null)
                        {
                            count++;
                            Console.WriteLine($"Successfully created wall {count}");
                        }
                        else
                        {
                            Console.WriteLine("Error: RAM returned null wall");
                        }

                        // If the wall has more than 2 points, create additional wall segments
                        for (int i = 1; i < wall.Points.Count - 1; i++)
                        {
                            Point2D currentPoint = wall.Points[i];
                            Point2D nextPoint = wall.Points[i + 1];

                            double curX = Helpers.ConvertToInches(currentPoint.X, _lengthUnit);
                            double curY = Helpers.ConvertToInches(currentPoint.Y, _lengthUnit);
                            double nextX = Helpers.ConvertToInches(nextPoint.X, _lengthUnit);
                            double nextY = Helpers.ConvertToInches(nextPoint.Y, _lengthUnit);

                            // Create additional wall segment
                            ramWall = layoutWalls.Add(
                                EMATERIALTYPES.EConcreteMat,
                                curX, curY, 0, 0,
                                nextX, nextY, 0, 0,
                                thickness);

                            if (ramWall != null)
                            {
                                count++;
                                Console.WriteLine($"Successfully created wall segment {count}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating wall: {ex.Message}");
                        // Continue with next wall instead of failing the whole import
                    }
                }

                Console.WriteLine($"Successfully imported {count} wall segments");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing walls: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
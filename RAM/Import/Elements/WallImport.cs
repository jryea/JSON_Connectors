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
    public class WallImport
    {
        private IModel _model;
        private string _lengthUnit;

        public WallImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Wall> walls, IEnumerable<Level> levels, IEnumerable<WallProperties> wallProperties)
        {
            try
            {
                if (walls == null || !walls.Any() || levels == null || !levels.Any())
                    return 0;

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                // Create a map of level IDs to Level objects
                Dictionary<string, Level> levelById = levels.ToDictionary(l => l.Id);

                // Sort levels by elevation
                var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

                // Map floor types to their "master" level (first level with that floor type)
                Dictionary<string, Level> masterLevelByFloorTypeId = new Dictionary<string, Level>();

                // Find the first (lowest) level for each floor type - this will be the "master" level
                foreach (var level in sortedLevels)
                {
                    if (!string.IsNullOrEmpty(level.FloorTypeId) && !masterLevelByFloorTypeId.ContainsKey(level.FloorTypeId))
                    {
                        masterLevelByFloorTypeId[level.FloorTypeId] = level;
                        Console.WriteLine($"Floor type {level.FloorTypeId} has master level {level.Name} at elevation {level.Elevation}");
                    }
                }

                // Map Core floor types to RAM floor types
                Dictionary<string, IFloorType> ramFloorTypeByFloorTypeId = new Dictionary<string, IFloorType>();

                // Assign RAM floor types to Core floor types
                for (int i = 0; i < ramFloorTypes.GetCount() && i < masterLevelByFloorTypeId.Count; i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    string coreFloorTypeId = masterLevelByFloorTypeId.Keys.ElementAt(i);
                    ramFloorTypeByFloorTypeId[coreFloorTypeId] = ramFloorType;
                    Console.WriteLine($"Mapped Core floor type {coreFloorTypeId} to RAM floor type {ramFloorType.strLabel}");
                }

                // Map wall properties for thickness
                Dictionary<string, double> wallThicknessByPropId = new Dictionary<string, double>();
                foreach (var wallProp in wallProperties ?? Enumerable.Empty<WallProperties>())
                {
                    if (!string.IsNullOrEmpty(wallProp.Id))
                    {
                        wallThicknessByPropId[wallProp.Id] = wallProp.Thickness;
                    }
                }

                // Track processed wall segments to avoid duplicates
                HashSet<string> processedWallSegments = new HashSet<string>();

                // Process walls
                int count = 0;
                foreach (var wall in walls)
                {
                    if (wall.Points == null || wall.Points.Count < 2)
                    {
                        Console.WriteLine($"Skipping wall {wall.Id}: Insufficient points defined");
                        continue;
                    }

                    // Get the TOP level for this wall (similar to beam's level)
                    Level topLevel = null;
                    if (!string.IsNullOrEmpty(wall.TopLevelId) && levelById.TryGetValue(wall.TopLevelId, out topLevel))
                    {
                        // Great, we have the top level
                    }
                    else
                    {
                        // If no top level specified, try to find highest level
                        topLevel = sortedLevels.LastOrDefault();
                        if (topLevel == null)
                        {
                            Console.WriteLine($"Skipping wall {wall.Id}: No levels found");
                            continue;
                        }
                    }

                    // Get the floor type ID for the top level
                    string floorTypeId = topLevel.FloorTypeId;
                    if (string.IsNullOrEmpty(floorTypeId))
                    {
                        Console.WriteLine($"Skipping wall {wall.Id}: Top level has no floor type");
                        continue;
                    }

                    // Check if this is the master level for this floor type
                    if (!masterLevelByFloorTypeId.TryGetValue(floorTypeId, out Level masterLevel))
                    {
                        Console.WriteLine($"Skipping wall {wall.Id}: No master level found for floor type {floorTypeId}");
                        continue;
                    }

                    // Get the RAM floor type
                    if (!ramFloorTypeByFloorTypeId.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        Console.WriteLine($"Skipping wall {wall.Id}: No RAM floor type found for floor type {floorTypeId}");
                        continue;
                    }

                    // Get wall thickness
                    double thickness = 8.0; // Default thickness in inches
                    if (!string.IsNullOrEmpty(wall.PropertiesId) &&
                        wallThicknessByPropId.TryGetValue(wall.PropertiesId, out double wallThickness))
                    {
                        thickness = Helpers.ConvertToInches(wallThickness, _lengthUnit);
                    }

                    ILayoutWalls layoutWalls = ramFloorType.GetLayoutWalls();
                    if (layoutWalls == null)
                    {
                        Console.WriteLine($"Skipping wall {wall.Id}: Could not get layout walls for floor type {ramFloorType.strLabel}");
                        continue;
                    }

                    // Create wall segments from points
                    for (int i = 0; i < wall.Points.Count - 1; i++)
                    {
                        Point2D startPoint = wall.Points[i];
                        Point2D endPoint = wall.Points[i + 1];

                        // Convert coordinates to inches
                        double startX = Helpers.ConvertToInches(startPoint.X, _lengthUnit);
                        double startY = Helpers.ConvertToInches(startPoint.Y, _lengthUnit);
                        double endX = Helpers.ConvertToInches(endPoint.X, _lengthUnit);
                        double endY = Helpers.ConvertToInches(endPoint.Y, _lengthUnit);

                        // Create a unique key for this wall segment
                        string wallSegmentKey = $"{startX:F2}_{startY:F2}_{endX:F2}_{endY:F2}_{thickness:F2}_{floorTypeId}";

                        // Skip if we've already processed this exact wall segment on this floor type
                        if (processedWallSegments.Contains(wallSegmentKey))
                        {
                            Console.WriteLine($"Skipping duplicate wall segment in floor type {ramFloorType.strLabel}");
                            continue;
                        }

                        // Add to processed set
                        processedWallSegments.Add(wallSegmentKey);

                        try
                        {
                            ILayoutWall ramWall = layoutWalls.Add(
                                EMATERIALTYPES.EConcreteMat,
                                startX, startY, 0, 0,
                                endX, endY, 0, 0,
                                thickness);

                            if (ramWall != null)
                            {
                                count++;
                                Console.WriteLine($"Added wall segment to floor type {ramFloorType.strLabel} (master level: {masterLevel.Name})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating wall segment: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"Successfully imported {count} wall segments");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing walls: {ex.Message}");
                throw;
            }
        }
    }
}
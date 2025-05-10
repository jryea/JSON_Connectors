using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    public class WallImport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public WallImport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Wall> walls, IEnumerable<Level> validLevels,
                          Dictionary<string, string> levelToFloorTypeMapping,
                          IEnumerable<WallProperties> wallProperties)
        {
            try
            {
                if (walls == null || !walls.Any() || validLevels == null || !validLevels.Any())
                    return 0;

                // Create a dictionary to map WallProperties by their ID
                var wallPropertiesById = wallProperties?.ToDictionary(wp => wp.Id) ?? new Dictionary<string, WallProperties>();

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                // Map Core floor types to RAM floor types
                Dictionary<string, IFloorType> ramFloorTypeByFloorTypeId = new Dictionary<string, IFloorType>();

                // Assign RAM floor types to Core floor types
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    string coreFloorTypeId = levelToFloorTypeMapping.Values.ElementAtOrDefault(i);
                    if (!string.IsNullOrEmpty(coreFloorTypeId))
                    {
                        ramFloorTypeByFloorTypeId[coreFloorTypeId] = ramFloorType;
                        Console.WriteLine($"Mapped Core floor type {coreFloorTypeId} to RAM floor type {ramFloorType.strLabel}");
                    }
                }

                // Track processed walls per floor type to avoid duplicates
                Dictionary<string, HashSet<string>> processedWallsByFloorType = new Dictionary<string, HashSet<string>>();

                // Import walls
                int count = 0;
                foreach (var wall in walls)
                {
                    if (string.IsNullOrEmpty(wall.TopLevelId) || wall.Points == null || wall.Points.Count < 2)
                        continue;

                    // Get the floor type ID for the wall's base level
                    if (!levelToFloorTypeMapping.TryGetValue(wall.TopLevelId, out string floorTypeId) ||
                        string.IsNullOrEmpty(floorTypeId))
                    {
                        Console.WriteLine($"No floor type mapping found for base level {wall.TopLevelId}");
                        continue;
                    }

                    // Get RAM floor type for this floor type
                    if (!ramFloorTypeByFloorTypeId.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        Console.WriteLine($"No RAM floor type found for floor type {floorTypeId}");
                        continue;
                    }

                    // Retrieve the wall thickness from WallProperties
                    double thickness = 0.0;
                    if (!string.IsNullOrEmpty(wall.PropertiesId) &&
                        wallPropertiesById.TryGetValue(wall.PropertiesId, out WallProperties wallProp))
                    {
                        thickness = UnitConversionUtils.ConvertToInches(wallProp.Thickness, _lengthUnit);
                    }
                    else
                    {
                        Console.WriteLine($"No WallProperties found for wall {wall.Id}");
                        continue;
                    }

                    // Convert coordinates
                    var startPoint = wall.Points.First();
                    var endPoint = wall.Points.Last();
                    double x1 = UnitConversionUtils.ConvertToInches(startPoint.X, _lengthUnit);
                    double y1 = UnitConversionUtils.ConvertToInches(startPoint.Y, _lengthUnit);
                    double x2 = UnitConversionUtils.ConvertToInches(endPoint.X, _lengthUnit);
                    double y2 = UnitConversionUtils.ConvertToInches(endPoint.Y, _lengthUnit);

                    // Create a unique key for this wall
                    string wallKey = $"{x1:F2}_{y1:F2}_{x2:F2}_{y2:F2}_{floorTypeId}";

                    // Check if this wall already exists in this floor type
                    if (!processedWallsByFloorType.TryGetValue(floorTypeId, out var processedWalls))
                    {
                        processedWalls = new HashSet<string>();
                        processedWallsByFloorType[floorTypeId] = processedWalls;
                    }

                    if (processedWalls.Contains(wallKey))
                    {
                        Console.WriteLine($"Skipping duplicate wall on floor type {floorTypeId}");
                        continue;
                    }

                    // Add the wall to the processed set
                    processedWalls.Add(wallKey);

                    try
                    {
                        ILayoutWalls layoutWalls = ramFloorType.GetLayoutWalls();
                        if (layoutWalls != null)
                        {
                            ILayoutWall ramWall = layoutWalls.Add(
                                EMATERIALTYPES.EWallPropConcreteMat, // Always use concrete material for walls
                                x1, y1, 0, 0,
                                x2, y2, 0, 0,
                                thickness);

                            if (ramWall != null)
                            {
                                count++;
                                Console.WriteLine($"Added wall to floor type {ramFloorType.strLabel} with thickness {thickness}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating wall: {ex.Message}");
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
    }
}
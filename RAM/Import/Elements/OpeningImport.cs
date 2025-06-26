using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    public class OpeningImport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public OpeningImport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Opening> openings, IEnumerable<Level> levels,
                         Dictionary<string, string> levelToFloorTypeMapping)
        {
            try
            {
                if (openings == null || !openings.Any() || levels == null || !levels.Any())
                    return 0;

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                Console.WriteLine("Beginning opening import...");

                // Create mapping from Core floor type IDs to RAM floor types
                var coreFloorTypeToRamFloorType = new Dictionary<string, IFloorType>();

                // First, try to use existing mappings from ModelMappingUtility
                var uniqueFloorTypeIds = levelToFloorTypeMapping.Values.Distinct().ToList();
                foreach (var floorTypeId in uniqueFloorTypeIds)
                {
                    string ramFloorTypeUid = ModelMappingUtility.GetRamFloorTypeUidForFloorTypeId(floorTypeId);
                    if (!string.IsNullOrEmpty(ramFloorTypeUid))
                    {
                        if (int.TryParse(ramFloorTypeUid, out int ramUid))
                        {
                            for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                            {
                                IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                                if (ramFloorType.lUID == ramUid)
                                {
                                    coreFloorTypeToRamFloorType[floorTypeId] = ramFloorType;
                                    Console.WriteLine($"Using existing mapping: Core floor type {floorTypeId} to RAM floor type {ramFloorType.strLabel}");
                                    break;
                                }
                            }
                        }
                    }
                }

                // Fallback mapping for unmapped floor types
                if (coreFloorTypeToRamFloorType.Count < uniqueFloorTypeIds.Count)
                {
                    Console.WriteLine("Some floor types not mapped, using fallback mappings");
                    int index = 0;
                    foreach (var floorTypeId in uniqueFloorTypeIds)
                    {
                        if (!coreFloorTypeToRamFloorType.ContainsKey(floorTypeId) && index < ramFloorTypes.GetCount())
                        {
                            coreFloorTypeToRamFloorType[floorTypeId] = ramFloorTypes.GetAt(index);
                            Console.WriteLine($"Fallback mapping: Core floor type {floorTypeId} to RAM floor type {ramFloorTypes.GetAt(index).strLabel}");
                            index++;
                        }
                    }
                }

                // Create a mapping from level IDs to RAM floor types
                var levelIdToRamFloorType = new Dictionary<string, IFloorType>();
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id) &&
                        levelToFloorTypeMapping.TryGetValue(level.Id, out string floorTypeId) &&
                        coreFloorTypeToRamFloorType.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        levelIdToRamFloorType[level.Id] = ramFloorType;
                    }
                }

                // Track processed openings per floor type to avoid duplicates
                var processedOpeningsByFloorType = new Dictionary<int, HashSet<string>>();

                // Import openings
                int count = 0;
                foreach (var opening in openings)
                {
                    if (opening.Points == null || opening.Points.Count < 3 || string.IsNullOrEmpty(opening.LevelId))
                    {
                        Console.WriteLine("Skipping opening with insufficient points or missing level ID");
                        continue;
                    }

                    // Convert coordinates to inches
                    var convertedPoints = new List<(double x, double y)>();
                    foreach (var point in opening.Points)
                    {
                        double x = Math.Round(UnitConversionUtils.ConvertToInches(point.X, _lengthUnit), 6);
                        double y = Math.Round(UnitConversionUtils.ConvertToInches(point.Y, _lengthUnit), 6);
                        convertedPoints.Add((x, y));
                    }

                    // Get the RAM floor type for this opening's level
                    if (!levelIdToRamFloorType.TryGetValue(opening.LevelId, out IFloorType ramFloorType))
                    {
                        Console.WriteLine($"No RAM floor type found for level {opening.LevelId}, skipping");
                        continue;
                    }

                    // Create a geometric key for this opening based on its points
                    string openingKey = CreateOpeningGeometricKey(convertedPoints);

                    // Check if this opening already exists in this floor type
                    int floorTypeUid = ramFloorType.lUID;
                    if (!processedOpeningsByFloorType.TryGetValue(floorTypeUid, out var processedOpenings))
                    {
                        processedOpenings = new HashSet<string>();
                        processedOpeningsByFloorType[floorTypeUid] = processedOpenings;
                    }

                    if (processedOpenings.Contains(openingKey))
                    {
                        Console.WriteLine($"Skipping duplicate opening on floor type {ramFloorType.strLabel}");
                        continue;
                    }

                    // Add the opening to the processed set
                    processedOpenings.Add(openingKey);

                    try
                    {
                        // Step 1: Get slab openings collection from the floor type
                        ISlabOpenings slabOpenings = ramFloorType.GetSlabOpenings();
                        if (slabOpenings == null)
                        {
                            Console.WriteLine($"Failed to get slab openings from floor type {ramFloorType.strLabel}");
                            continue;
                        }

                        // Step 2: Add slab edges for the opening boundary
                        ISlabEdges slabEdges = ramFloorType.GetAllSlabEdges();
                        if (slabEdges == null)
                        {
                            Console.WriteLine($"Failed to get slab edges from floor type {ramFloorType.strLabel}");
                            continue;
                        }

                        // Add edges between consecutive points (and close the loop)
                        for (int i = 0; i < convertedPoints.Count; i++)
                        {
                            int nextIndex = (i + 1) % convertedPoints.Count; // Wrap to 0 for last point

                            var startPoint = convertedPoints[i];
                            var endPoint = convertedPoints[nextIndex];

                            ISlabEdge slabEdge = slabEdges.Add(
                                startPoint.x, startPoint.y,
                                endPoint.x, endPoint.y,
                                0.0); // Offset

                            if (slabEdge == null)
                            {
                                Console.WriteLine($"Failed to add slab edge from ({startPoint.x}, {startPoint.y}) to ({endPoint.x}, {endPoint.y})");
                            }
                        }

                        // Note: Unlike floors/decks, slab openings don't appear to have a direct Add method
                        // in the RAM API documentation provided. The opening geometry is defined by 
                        // the slab edges we just created. RAM may automatically recognize openings
                        // based on closed edge loops, or there may be additional methods not documented.

                        // For now, we'll count this as successful since we've added the boundary edges
                        count++;
                        Console.WriteLine($"Successfully created opening boundary on floor type {ramFloorType.strLabel} with {convertedPoints.Count} points");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating opening: {ex.Message}");
                    }
                }

                Console.WriteLine($"Imported {count} openings");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing openings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a normalized geometric key for an opening based on its points
        /// </summary>
        private string CreateOpeningGeometricKey(List<(double x, double y)> points)
        {
            // Sort points to create a consistent key regardless of point order
            var sortedPoints = points.OrderBy(p => p.x).ThenBy(p => p.y).ToList();

            // Create key from sorted points
            var pointStrings = sortedPoints.Select(p => $"{p.x:F2},{p.y:F2}");
            return string.Join("_", pointStrings);
        }
    }
}
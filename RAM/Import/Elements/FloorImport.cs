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
    public class FloorImport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public FloorImport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Floor> floors, IEnumerable<Level> levels,
                         Dictionary<string, string> levelToFloorTypeMapping,
                         Dictionary<string, int> floorPropertyMappings)
        {
            try
            {
                if (floors == null || !floors.Any() || levels == null || !levels.Any())
                    return 0;

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                Console.WriteLine("Beginning floor import...");

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

                // Track processed floors per floor type to avoid duplicates
                var processedFloorsByFloorType = new Dictionary<int, HashSet<string>>();

                // Import floors
                int count = 0;
                foreach (var floor in floors)
                {
                    if (floor.Points == null || floor.Points.Count < 3 || string.IsNullOrEmpty(floor.LevelId))
                    {
                        Console.WriteLine("Skipping floor with insufficient points or missing level ID");
                        continue;
                    }

                    // Convert coordinates to inches
                    var convertedPoints = new List<(double x, double y)>();
                    foreach (var point in floor.Points)
                    {
                        double x = Math.Round(UnitConversionUtils.ConvertToInches(point.X, _lengthUnit), 6);
                        double y = Math.Round(UnitConversionUtils.ConvertToInches(point.Y, _lengthUnit), 6);
                        convertedPoints.Add((x, y));
                    }

                    // Get the RAM floor type for this floor's level
                    if (!levelIdToRamFloorType.TryGetValue(floor.LevelId, out IFloorType ramFloorType))
                    {
                        Console.WriteLine($"No RAM floor type found for level {floor.LevelId}, skipping");
                        continue;
                    }

                    // Create a geometric key for this floor based on its points
                    string floorKey = CreateFloorGeometricKey(convertedPoints);

                    // Check if this floor already exists in this floor type
                    int floorTypeUid = ramFloorType.lUID;
                    if (!processedFloorsByFloorType.TryGetValue(floorTypeUid, out var processedFloors))
                    {
                        processedFloors = new HashSet<string>();
                        processedFloorsByFloorType[floorTypeUid] = processedFloors;
                    }

                    if (processedFloors.Contains(floorKey))
                    {
                        Console.WriteLine($"Skipping duplicate floor on floor type {ramFloorType.strLabel}");
                        continue;
                    }

                    // Add the floor to the processed set
                    processedFloors.Add(floorKey);

                    try
                    {
                        // Step 1: Get existing deck/slab property from FloorPropertiesImport mappings
                        int ramPropertyUid = 0;
                        if (!string.IsNullOrEmpty(floor.FloorPropertiesId) &&
                            floorPropertyMappings.TryGetValue(floor.FloorPropertiesId, out ramPropertyUid))
                        {
                            Console.WriteLine($"Using existing RAM property UID {ramPropertyUid} for floor properties {floor.FloorPropertiesId}");
                        }
                        else
                        {
                            Console.WriteLine($"No RAM property mapping found for floor properties {floor.FloorPropertiesId}");

                            // Debug: Print available mappings
                            Console.WriteLine($"Available mappings: {string.Join(", ", floorPropertyMappings.Keys)}");

                            // Fallback: Get first available property from any type
                            ramPropertyUid = GetFirstAvailableFloorProperty();

                            if (ramPropertyUid == 0)
                            {
                                Console.WriteLine($"No floor properties found, creating generic concrete slab");
                                ramPropertyUid = CreateGenericConcreteSlab();
                            }

                            if (ramPropertyUid == 0)
                            {
                                Console.WriteLine($"Failed to get or create floor property, skipping floor");
                                continue;
                            }

                            Console.WriteLine($"Using fallback RAM property UID {ramPropertyUid}");
                        }

                        // Step 2: Add slab edges using IFloorType.GetAllSlabEdges()
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

                        // Step 3: Create a deck using IFloorType.GetDecks() with existing property UID
                        IDecks decks = ramFloorType.GetDecks();
                        if (decks == null)
                        {
                            Console.WriteLine($"Failed to get decks from floor type {ramFloorType.strLabel}");
                            continue;
                        }

                        IDeck deck = decks.Add(ramPropertyUid, convertedPoints.Count);
                        if (deck == null)
                        {
                            Console.WriteLine($"Failed to create deck on floor type {ramFloorType.strLabel} with property UID {ramPropertyUid}");
                            continue;
                        }

                        // Step 4: Set deck properties
                        deck.eSlabAction = ESlabActions.eDSAOneWay;
                        deck.dAngle = floor.SpanDirection; // Use span direction from floor

                        // Step 5: Set deck points
                        IPoints points = deck.GetPoints();
                        if (points != null)
                        {
                            // Delete existing points and insert new ones
                            for (int i = 0; i < convertedPoints.Count; i++)
                            {
                                if (points.GetCount() > i)
                                {
                                    points.Delete(i);
                                }
                                points.InsertAt2(i, convertedPoints[i].x, convertedPoints[i].y, 0);
                            }

                            // Set the points back to the deck
                            deck.SetPoints(points);
                        }

                        count++;
                        Console.WriteLine($"Successfully created floor deck on floor type {ramFloorType.strLabel} with property UID {ramPropertyUid} and {convertedPoints.Count} points");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating floor: {ex.Message}");
                    }
                }

                Console.WriteLine($"Imported {count} floors");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing floors: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a normalized geometric key for a floor based on its points
        /// </summary>
        private string CreateFloorGeometricKey(List<(double x, double y)> points)
        {
            // Sort points to create a consistent key regardless of point order
            var sortedPoints = points.OrderBy(p => p.x).ThenBy(p => p.y).ToList();

            // Create key from sorted points
            var pointStrings = sortedPoints.Select(p => $"{p.x:F2},{p.y:F2}");
            return string.Join("_", pointStrings);
        }

        /// <summary>
        /// Gets the first available floor property UID from any type (slab, comp deck, non-comp deck)
        /// </summary>
        private int GetFirstAvailableFloorProperty()
        {
            // Try concrete slabs first
            IConcSlabProps concSlabs = _model.GetConcreteSlabProps();
            if (concSlabs != null && concSlabs.GetCount() > 0)
            {
                return concSlabs.GetAt(0).lUID;
            }

            // Try composite decks
            ICompDeckProps compDecks = _model.GetCompositeDeckProps();
            if (compDecks != null && compDecks.GetCount() > 0)
            {
                return compDecks.GetAt(0).lUID;
            }

            // Try non-composite decks
            INonCompDeckProps nonCompDecks = _model.GetNonCompDeckProps();
            if (nonCompDecks != null && nonCompDecks.GetCount() > 0)
            {
                return nonCompDecks.GetAt(0).lUID;
            }

            return 0; // No properties found
        }

        /// <summary>
        /// Creates a generic concrete slab property as fallback
        /// </summary>
        private int CreateGenericConcreteSlab()
        {
            try
            {
                IConcSlabProps concSlabs = _model.GetConcreteSlabProps();
                if (concSlabs != null)
                {
                    IConcSlabProp genericSlab = concSlabs.Add(
                        "Generic Slab",     // Name
                        6.0,               // Thickness (6 inches)
                        75.0);             // Self weight (6" * 150pcf / 12)

                    if (genericSlab != null)
                    {
                        Console.WriteLine($"Created generic concrete slab with UID {genericSlab.lUID}");
                        return genericSlab.lUID;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating generic concrete slab: {ex.Message}");
            }

            return 0; // Failed to create
        }
    }
}
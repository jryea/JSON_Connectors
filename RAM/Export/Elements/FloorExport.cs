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
            Console.WriteLine("Starting Floor export from RAM");

            try
            {
                IFloorTypes floorTypes = _model.GetFloorTypes();
                if (floorTypes == null || floorTypes.GetCount() == 0)
                {
                    Console.WriteLine("ERROR: No floor types found in RAM model");
                    return floors;
                }

                Console.WriteLine($"Found {floorTypes.GetCount()} floor types in RAM model");

                // Process each floor type
                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType floorType = floorTypes.GetAt(i);
                    if (floorType == null)
                    {
                        Console.WriteLine($"ERROR: Floor type at index {i} is null");
                        continue;
                    }

                    Console.WriteLine($"Processing floor type: {floorType.strLabel} (UID: {floorType.lUID})");

                    // Find floor property ID (if available)
                    string floorPropertiesId = ModelMappingUtility.GetFloorTypeIdForUid(floorType.lUID.ToString());
                    Console.WriteLine($"Floor properties ID mapping: {(string.IsNullOrEmpty(floorPropertiesId) ? "NOT FOUND" : floorPropertiesId)}");

                    // Get slab perimeters for this floor type
                    ISlabPerimeters slabPerimeters = floorType.GetSlabPerimeters();
                    if (slabPerimeters == null)
                    {
                        Console.WriteLine($"ERROR: Slab perimeters object is null for floor type {floorType.strLabel}");
                        continue;
                    }

                    Console.WriteLine($"Found {slabPerimeters.GetCount()} slab perimeters for floor type {floorType.strLabel}");

                    if (slabPerimeters.GetCount() == 0)
                    {
                        Console.WriteLine($"WARNING: No slab perimeters found for floor type {floorType.strLabel}");
                        continue;
                    }

                    // Process each story that uses this floor type
                    IStories ramStories = _model.GetStories();
                    if (ramStories == null || ramStories.GetCount() == 0)
                    {
                        Console.WriteLine("ERROR: No stories found in RAM model");
                        continue;
                    }

                    Console.WriteLine($"Checking {ramStories.GetCount()} stories for floor type {floorType.strLabel}");
                    bool foundMatchingStory = false;

                    for (int k = 0; k < ramStories.GetCount(); k++)
                    {
                        IStory ramStory = ramStories.GetAt(k);
                        if (ramStory == null)
                        {
                            Console.WriteLine($"ERROR: Story at index {k} is null");
                            continue;
                        }

                        if (ramStory.GetFloorType() == null)
                        {
                            Console.WriteLine($"WARNING: Story {ramStory.strLabel} has no floor type");
                            continue;
                        }

                        if (ramStory.GetFloorType().lUID != floorType.lUID)
                        {
                            Console.WriteLine($"Story {ramStory.strLabel} uses different floor type: {ramStory.GetFloorType().strLabel}");
                            continue;
                        }

                        Console.WriteLine($"Found matching story: {ramStory.strLabel} (UID: {ramStory.lUID}) using floor type {floorType.strLabel}");
                        foundMatchingStory = true;

                        // Find the corresponding level ID for this story
                        string levelId = ModelMappingUtility.GetLevelIdForStoryUid(ramStory.lUID.ToString());
                        if (string.IsNullOrEmpty(levelId))
                        {
                            Console.WriteLine($"ERROR: No level mapping found for story {ramStory.strLabel} (UID: {ramStory.lUID})");

                            // Debug the mapping dictionary contents
                            Console.WriteLine("Debugging mappings in ModelMappingUtility:");
                            PrintMappingDictionaries();
                            continue;
                        }

                        Console.WriteLine($"Found level ID for story {ramStory.strLabel}: {levelId}");

                        // Process each slab perimeter
                        for (int j = 0; j < slabPerimeters.GetCount(); j++)
                        {
                            ISlabPerimeter slabPerimeter = slabPerimeters.GetAt(j);
                            if (slabPerimeter == null)
                            {
                                Console.WriteLine($"ERROR: Slab perimeter at index {j} is null");
                                continue;
                            }

                            List<Point2D> floorPoints = new List<Point2D>();

                            // Get points for the slab perimeter
                            IPoints slabPerimeterPoints = slabPerimeter.GetPerimeterVertices();
                            if (slabPerimeterPoints == null)
                            {
                                Console.WriteLine($"ERROR: Perimeter vertices object is null for slab perimeter {j}");
                                continue;
                            }

                            if (slabPerimeterPoints.GetCount() < 3)
                            {
                                Console.WriteLine($"WARNING: Slab perimeter {j} has only {slabPerimeterPoints.GetCount()} points (minimum 3 required)");
                                continue;
                            }

                            Console.WriteLine($"Slab perimeter {j} has {slabPerimeterPoints.GetCount()} points");

                            // Extract points from the perimeter
                            for (int p = 0; p < slabPerimeterPoints.GetCount(); p++)
                            {
                                IPoint slabPerimeterPoint = slabPerimeterPoints.GetAt(p);
                                if (slabPerimeterPoint == null)
                                {
                                    Console.WriteLine($"ERROR: Perimeter point at index {p} is null");
                                    continue;
                                }

                                // Get the coordinates of the slab perimeter point
                                SCoordinate slabPoint = new SCoordinate();
                                slabPerimeterPoint.GetCoordinate(ref slabPoint);

                                // Convert to Point2D
                                Point2D point2D = new Point2D(
                                    UnitConversionUtils.ConvertFromInches(slabPoint.dXLoc, "inches"),
                                    UnitConversionUtils.ConvertFromInches(slabPoint.dYLoc, "inches")
                                );

                                floorPoints.Add(point2D);
                                // Console.WriteLine($"  Point {p}: ({point2D.X}, {point2D.Y})");
                            }

                            // Skip floors with insufficient points
                            if (floorPoints.Count < 3)
                            {
                                Console.WriteLine($"WARNING: Not enough valid points for floor from slab perimeter {j} (only {floorPoints.Count}, minimum 3 required)");
                                continue;
                            }

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
                            Console.WriteLine($"SUCCESS: Created floor with ID {floor.Id} at level {levelId} with {floorPoints.Count} points");
                        }
                    }

                    if (!foundMatchingStory)
                    {
                        Console.WriteLine($"WARNING: No stories found using floor type {floorType.strLabel}");
                    }
                }

                Console.WriteLine($"Floor export complete. Created {floors.Count} floors.");
                return floors;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR exporting floors from RAM: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return floors;
            }
        }

        // Helper method to print mapping dictionaries for debugging
        private void PrintMappingDictionaries()
        {
            // Use reflection to access private static fields in ModelMappingUtility
            Type type = typeof(ModelMappingUtility);
            var fields = type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            foreach (var field in fields)
            {
                if (field.Name.EndsWith("Uid") || field.Name.EndsWith("Id"))
                {
                    Console.WriteLine($"Dictionary {field.Name}:");
                    var dictionary = field.GetValue(null);
                    if (dictionary != null)
                    {
                        var count = (int)dictionary.GetType().GetProperty("Count").GetValue(dictionary);
                        Console.WriteLine($"  Contains {count} entries");

                        // If the dictionary is small enough, print its contents
                        if (count < 10)
                        {
                            var entries = dictionary.GetType().GetMethod("GetEnumerator").Invoke(dictionary, null);
                            var moveNext = entries.GetType().GetMethod("MoveNext");
                            var current = entries.GetType().GetProperty("Current");

                            while ((bool)moveNext.Invoke(entries, null))
                            {
                                var entry = current.GetValue(entries);
                                var key = entry.GetType().GetProperty("Key").GetValue(entry);
                                var value = entry.GetType().GetProperty("Value").GetValue(entry);
                                Console.WriteLine($"    {key} -> {value}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("  Dictionary is null");
                    }
                }
            }
        }

    }
}
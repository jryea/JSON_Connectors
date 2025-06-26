using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

                    // Get decks for this floor type
                    IDecks decks = floorType.GetDecks();
                    if (decks == null || decks.GetCount() == 0)
                    {
                        Console.WriteLine($"No decks found for floor type {floorType.strLabel}");
                        continue;
                    }

                    Console.WriteLine($"Found {decks.GetCount()} deck(s) for floor type {floorType.strLabel}");

                    // Find stories that use this floor type
                    IStories ramStories = _model.GetStories();
                    if (ramStories == null || ramStories.GetCount() == 0)
                    {
                        Console.WriteLine("No stories found in RAM model");
                        continue;
                    }

                    bool foundMatchingStory = false;

                    // Check each story to see if it uses this floor type
                    for (int storyIdx = 0; storyIdx < ramStories.GetCount(); storyIdx++)
                    {
                        IStory ramStory = ramStories.GetAt(storyIdx);
                        if (ramStory == null || ramStory.GetFloorType() == null)
                            continue;

                        if (ramStory.GetFloorType().lUID != floorType.lUID)
                            continue;

                        Console.WriteLine($"Found matching story: {ramStory.strLabel} (UID: {ramStory.lUID}) using floor type {floorType.strLabel}");
                        foundMatchingStory = true;

                        // Find the corresponding level ID for this story using ModelMappingUtility
                        string levelId = ModelMappingUtility.GetLevelIdForStoryUid(ramStory.lUID.ToString());
                        if (string.IsNullOrEmpty(levelId))
                        {
                            Console.WriteLine($"ERROR: No level mapping found for story {ramStory.strLabel} (UID: {ramStory.lUID})");
                            continue;
                        }

                        Console.WriteLine($"Found level ID for story {ramStory.strLabel}: {levelId}");

                        // Process each deck in this floor type
                        for (int deckIdx = 0; deckIdx < decks.GetCount(); deckIdx++)
                        {
                            IDeck deck = decks.GetAt(deckIdx);
                            if (deck == null)
                            {
                                Console.WriteLine($"ERROR: Deck at index {deckIdx} is null");
                                continue;
                            }

                            Console.WriteLine($"Processing deck with property ID: {deck.lUID}");

                            // Use ModelMappingUtility to get FloorProperties ID from deck property UID
                            string floorPropertiesId = ModelMappingUtility.GetFloorPropertiesIdForUid(deck.lUID.ToString());
                            if (string.IsNullOrEmpty(floorPropertiesId))
                            {
                                Console.WriteLine($"ERROR: No FloorProperties mapping found for deck property UID {deck.lUID}");
                                continue;
                            }

                            Console.WriteLine($"Found FloorProperties ID: {floorPropertiesId}");

                            // Try to extract floor points using multiple methods
                            var floorPoints = ExtractFloorPoints(deck, floorType, deckIdx);

                            if (floorPoints.Count < 3)
                            {
                                Console.WriteLine($"WARNING: Not enough points for floor from deck {deckIdx} (only {floorPoints.Count}, minimum 3 required)");
                                continue;
                            }

                            // Create floor object
                            var floor = new Floor
                            {
                                Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR),
                                LevelId = levelId,
                                FloorPropertiesId = floorPropertiesId,
                                Points = floorPoints,
                                SpanDirection = deck.dAngle
                            };

                            floors.Add(floor);
                            Console.WriteLine($"Successfully created floor {floor.Id} with {floorPoints.Count} points on level {levelId}");
                        }
                    }

                    if (!foundMatchingStory)
                    {
                        Console.WriteLine($"No stories found using floor type {floorType.strLabel}");
                    }
                }

                Console.WriteLine($"Successfully created {floors.Count} floors.");
                return floors;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR exporting floors from RAM: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return floors;
            }
        }

        /// <summary>
        /// Extract floor points using multiple methods (deck points and slab edges)
        /// </summary>
        private List<Point2D> ExtractFloorPoints(IDeck deck, IFloorType floorType, int deckIdx)
        {
            Console.WriteLine($"\n=== EXTRACTING POINTS FOR DECK {deckIdx} ===");

            // Method 1: Try deck.GetPoints() (original method)
            var deckPoints = TryExtractPointsFromDeck(deck, deckIdx);

            // Method 2: Try slab edges if deck points are insufficient
            var edgePoints = new List<Point2D>();
            if (deckPoints.Count < 8) // Adjust this threshold as needed
            {
                Console.WriteLine($"Deck points ({deckPoints.Count}) may be insufficient, trying slab edges...");
                edgePoints = TryExtractPointsFromSlabEdges(floorType, deckIdx);
            }

            // Choose the best result
            List<Point2D> finalPoints;
            if (edgePoints.Count > deckPoints.Count)
            {
                Console.WriteLine($"Using slab edge points: {edgePoints.Count} points vs {deckPoints.Count} deck points");
                finalPoints = edgePoints;
            }
            else
            {
                Console.WriteLine($"Using deck points: {deckPoints.Count} points");
                finalPoints = deckPoints;
            }

            Console.WriteLine($"=== FINAL RESULT: {finalPoints.Count} POINTS ===\n");
            return finalPoints;
        }

        /// <summary>
        /// Extract points using the original deck.GetPoints() method with debugging and duplicate removal
        /// </summary>
        private List<Point2D> TryExtractPointsFromDeck(IDeck deck, int deckIdx)
        {
            var floorPoints = new List<Point2D>();

            try
            {
                Console.WriteLine($"=== METHOD 1: DECK.GETPOINTS() ===");

                // Get deck geometry points
                IPoints deckPoints = deck.GetPoints();

                int reportedPointCount = deckPoints?.GetCount() ?? 0;
                Console.WriteLine($"RAM reports {reportedPointCount} points for deck {deckIdx}");

                if (deckPoints == null || deckPoints.GetCount() < 3)
                {
                    Console.WriteLine($"Insufficient deck points: {reportedPointCount}");
                    return floorPoints;
                }

                // Extract points with debugging
                int nullPointCount = 0;
                int invalidCoordCount = 0;
                int validPointCount = 0;
                int exceptionCount = 0;

                for (int ptIdx = 0; ptIdx < deckPoints.GetCount(); ptIdx++)
                {
                    try
                    {
                        IPoint deckPoint = deckPoints.GetAt(ptIdx);

                        if (deckPoint == null)
                        {
                            Console.WriteLine($"  Point {ptIdx}: NULL POINT - skipped");
                            nullPointCount++;
                            continue;
                        }

                        // Get the coordinates
                        SCoordinate coord = new SCoordinate();
                        deckPoint.GetCoordinate(ref coord);

                        Console.WriteLine($"  Point {ptIdx}: Raw({coord.dXLoc:F3}, {coord.dYLoc:F3}, {coord.dZLoc:F3})");

                        // Check for invalid coordinates
                        if (double.IsNaN(coord.dXLoc) || double.IsNaN(coord.dYLoc) ||
                            double.IsInfinity(coord.dXLoc) || double.IsInfinity(coord.dYLoc))
                        {
                            Console.WriteLine($"  Point {ptIdx}: INVALID COORDINATES - skipped");
                            invalidCoordCount++;
                            continue;
                        }

                        var point = new Point2D(
                            UnitConversionUtils.ConvertFromInches(coord.dXLoc, _lengthUnit),
                            UnitConversionUtils.ConvertFromInches(coord.dYLoc, _lengthUnit)
                        );

                        Console.WriteLine($"  Point {ptIdx}: Converted({point.X:F3}, {point.Y:F3}) - ADDED");

                        floorPoints.Add(point);
                        validPointCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Point {ptIdx}: EXCEPTION - {ex.Message}");
                        exceptionCount++;
                        continue;
                    }
                }

                Console.WriteLine($"Deck extraction summary: {reportedPointCount} reported, {validPointCount} valid, {nullPointCount} null, {invalidCoordCount} invalid, {exceptionCount} exceptions");

                if (floorPoints.Count != reportedPointCount)
                {
                    int totalLost = nullPointCount + invalidCoordCount + exceptionCount;
                    Console.WriteLine($"*** MISMATCH: Expected {reportedPointCount}, got {floorPoints.Count} (lost {totalLost}) ***");
                }

                // Remove duplicate closing point if it exists
                floorPoints = RemoveDuplicateClosingPoint(floorPoints);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deck point extraction failed: {ex.Message}");
            }

            return floorPoints;
        }

        /// <summary>
        /// Remove duplicate closing point if the last point is the same as the first point
        /// </summary>
        private List<Point2D> RemoveDuplicateClosingPoint(List<Point2D> points)
        {
            if (points == null || points.Count < 4)
            {
                Console.WriteLine($"RemoveDuplicateClosingPoint: Not enough points ({points?.Count ?? 0}) to check for duplicates");
                return points ?? new List<Point2D>();
            }

            var firstPoint = points[0];
            var lastPoint = points[points.Count - 1];

            // Check if first and last points are the same (within tolerance)
            const double tolerance = 1e-6;
            double distance = Math.Sqrt(
                Math.Pow(firstPoint.X - lastPoint.X, 2) +
                Math.Pow(firstPoint.Y - lastPoint.Y, 2)
            );

            if (distance <= tolerance)
            {
                Console.WriteLine($"RemoveDuplicateClosingPoint: Found duplicate closing point. Distance: {distance:E3}");
                Console.WriteLine($"  First point: ({firstPoint.X:F6}, {firstPoint.Y:F6})");
                Console.WriteLine($"  Last point:  ({lastPoint.X:F6}, {lastPoint.Y:F6})");
                Console.WriteLine($"  Removing last point. Points: {points.Count} -> {points.Count - 1}");

                // Return new list without the last point
                return points.Take(points.Count - 1).ToList();
            }
            else
            {
                Console.WriteLine($"RemoveDuplicateClosingPoint: No duplicate closing point found. Distance: {distance:F6}");
                Console.WriteLine($"  First point: ({firstPoint.X:F6}, {firstPoint.Y:F6})");
                Console.WriteLine($"  Last point:  ({lastPoint.X:F6}, {lastPoint.Y:F6})");
                return points;
            }
        }

        /// <summary>
        /// Extract points from slab edges (alternative method based on FloorImport approach)
        /// </summary>
        private List<Point2D> TryExtractPointsFromSlabEdges(IFloorType floorType, int deckIdx)
        {
            var edgePoints = new List<Point2D>();

            try
            {
                Console.WriteLine($"=== METHOD 2: SLAB EDGES ===");

                // Get all slab edges for this floor type
                ISlabEdges slabEdges = floorType.GetAllSlabEdges();

                if (slabEdges == null)
                {
                    Console.WriteLine("No slab edges found");
                    return edgePoints;
                }

                int edgeCount = slabEdges.GetCount();
                Console.WriteLine($"Found {edgeCount} slab edges");

                if (edgeCount == 0)
                {
                    return edgePoints;
                }

                // Extract points from edges
                var allEdgePoints = new List<Point2D>();

                for (int i = 0; i < edgeCount; i++)
                {
                    ISlabEdge edge = slabEdges.GetAt(i);
                    if (edge == null)
                    {
                        Console.WriteLine($"  Edge {i}: NULL");
                        continue;
                    }

                    try
                    {
                        // Try to extract edge coordinates using reflection since method names may vary
                        var edgeCoords = TryGetEdgeCoordinates(edge, i);

                        if (edgeCoords.HasValue)
                        {
                            var (start, end) = edgeCoords.Value;

                            var startPoint = new Point2D(
                                UnitConversionUtils.ConvertFromInches(start.dXLoc, _lengthUnit),
                                UnitConversionUtils.ConvertFromInches(start.dYLoc, _lengthUnit)
                            );

                            var endPoint = new Point2D(
                                UnitConversionUtils.ConvertFromInches(end.dXLoc, _lengthUnit),
                                UnitConversionUtils.ConvertFromInches(end.dYLoc, _lengthUnit)
                            );

                            // Add unique points only
                            if (!allEdgePoints.Any(p => ArePointsEqual(p, startPoint)))
                            {
                                allEdgePoints.Add(startPoint);
                                Console.WriteLine($"  Edge {i}: Added start point ({startPoint.X:F3}, {startPoint.Y:F3})");
                            }

                            if (!allEdgePoints.Any(p => ArePointsEqual(p, endPoint)))
                            {
                                allEdgePoints.Add(endPoint);
                                Console.WriteLine($"  Edge {i}: Added end point ({endPoint.X:F3}, {endPoint.Y:F3})");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  Edge {i}: Could not extract coordinates");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Edge {i}: Error - {ex.Message}");
                    }
                }

                Console.WriteLine($"Slab edge extraction: {allEdgePoints.Count} unique points from {edgeCount} edges");
                edgePoints = allEdgePoints;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Slab edge extraction failed: {ex.Message}");
            }

            return edgePoints;
        }

        /// <summary>
        /// Try to get coordinates from a slab edge using various possible method names
        /// </summary>
        private (SCoordinate start, SCoordinate end)? TryGetEdgeCoordinates(ISlabEdge edge, int edgeIndex)
        {
            var edgeType = edge.GetType();

            // First, let's see ALL methods available on this edge object
            if (edgeIndex == 0) // Only do this detailed analysis for the first edge to avoid spam
            {
                Console.WriteLine($"\n=== DETAILED ANALYSIS OF EDGE 0 ===");
                Console.WriteLine($"Edge Type: {edgeType.FullName}");

                // Show ALL methods
                Console.WriteLine("\nALL METHODS:");
                var allMethods = edgeType.GetMethods()
                    .Where(m => m.DeclaringType != typeof(object)) // Exclude basic Object methods
                    .OrderBy(m => m.Name);

                foreach (var method in allMethods)
                {
                    var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"  {method.ReturnType.Name} {method.Name}({paramStr})");
                }

                // Show ALL properties
                Console.WriteLine("\nALL PROPERTIES:");
                var allProperties = edgeType.GetProperties()
                    .Where(p => p.DeclaringType != typeof(object))
                    .OrderBy(p => p.Name);

                foreach (var prop in allProperties)
                {
                    try
                    {
                        var value = prop.GetValue(edge);
                        Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name} = {value}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name} = ERROR: {ex.Message}");
                    }
                }
                Console.WriteLine("=== END DETAILED ANALYSIS ===\n");
            }

            // Try Method 1: Look for coordinate-related methods that take ref SCoordinate
            try
            {
                var coordinateMethods = edgeType.GetMethods()
                    .Where(m => m.Name.ToLower().Contains("coordinate") ||
                               m.Name.ToLower().Contains("start") ||
                               m.Name.ToLower().Contains("end") ||
                               m.Name.ToLower().Contains("point"))
                    .Where(m => m.GetParameters().Length == 1 &&
                               m.GetParameters()[0].ParameterType == typeof(SCoordinate).MakeByRefType())
                    .ToArray();

                foreach (var method in coordinateMethods)
                {
                    try
                    {
                        var coord = new SCoordinate();
                        object[] parameters = { coord };
                        method.Invoke(edge, parameters);
                        coord = (SCoordinate)parameters[0];

                        Console.WriteLine($"    -> {method.Name} returned: ({coord.dXLoc:F3}, {coord.dYLoc:F3}, {coord.dZLoc:F3})");

                        // Look for the corresponding end method if this looks like a start method
                        if (method.Name.ToLower().Contains("start"))
                        {
                            var endMethodName = method.Name.ToLower().Replace("start", "end");
                            var endMethod = edgeType.GetMethod(endMethodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (endMethod != null)
                            {
                                var endCoord = new SCoordinate();
                                object[] endParams = { endCoord };
                                endMethod.Invoke(edge, endParams);
                                endCoord = (SCoordinate)endParams[0];

                                Console.WriteLine($"    -> Found pair: Start({coord.dXLoc:F3}, {coord.dYLoc:F3}) End({endCoord.dXLoc:F3}, {endCoord.dYLoc:F3})");
                                return (coord, endCoord);
                            }
                        }
                        else if (method.Name.ToLower().Contains("end"))
                        {
                            // If we found an end method, look for corresponding start
                            var startMethodName = method.Name.ToLower().Replace("end", "start");
                            var startMethod = edgeType.GetMethod(startMethodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (startMethod != null)
                            {
                                var startCoord = new SCoordinate();
                                object[] startParams = { startCoord };
                                startMethod.Invoke(edge, startParams);
                                startCoord = (SCoordinate)startParams[0];

                                Console.WriteLine($"    -> Found pair: Start({startCoord.dXLoc:F3}, {startCoord.dYLoc:F3}) End({coord.dXLoc:F3}, {coord.dYLoc:F3})");
                                return (startCoord, coord);
                            }
                        }
                        else
                        {
                            // Try to find a matching pair by replacing "start" with "end" or vice versa
                            foreach (var suffix in new[] { "start", "end" })
                            {
                                var endMethodName = method.Name.ToLower().Replace(suffix == "start" ? "start" : "end", suffix == "start" ? "end" : "start");
                                var endMethod = edgeType.GetMethod(endMethodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                                if (endMethod != null)
                                {
                                    var endCoord = new SCoordinate();
                                    object[] endParams = { endCoord };
                                    endMethod.Invoke(edge, endParams);
                                    endCoord = (SCoordinate)endParams[0];

                                    Console.WriteLine($"    -> Found pair: Start({coord.dXLoc:F3}, {coord.dYLoc:F3}) End({endCoord.dXLoc:F3}, {endCoord.dYLoc:F3})");
                                    return (coord, endCoord);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    -> Failed to call {method.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Edge {edgeIndex}: Coordinate method search failed - {ex.Message}");
            }

            // Try Method 2: Look for X1, Y1, X2, Y2 style properties
            try
            {
                var coordProperties = edgeType.GetProperties()
                    .Where(p => p.Name.ToLower().Contains("x") ||
                               p.Name.ToLower().Contains("y") ||
                               p.Name.ToLower().Contains("start") ||
                               p.Name.ToLower().Contains("end"))
                    .ToArray();

                Console.WriteLine($"  Edge {edgeIndex}: Found {coordProperties.Length} coordinate-related properties:");

                double? x1 = null, y1 = null, x2 = null, y2 = null;

                foreach (var prop in coordProperties)
                {
                    try
                    {
                        var value = prop.GetValue(edge);
                        Console.WriteLine($"    {prop.Name} = {value}");

                        if (value is double doubleValue)
                        {
                            var propName = prop.Name.ToLower();
                            if (propName.Contains("x1") || (propName.Contains("x") && propName.Contains("start")))
                                x1 = doubleValue;
                            else if (propName.Contains("y1") || (propName.Contains("y") && propName.Contains("start")))
                                y1 = doubleValue;
                            else if (propName.Contains("x2") || (propName.Contains("x") && propName.Contains("end")))
                                x2 = doubleValue;
                            else if (propName.Contains("y2") || (propName.Contains("y") && propName.Contains("end")))
                                y2 = doubleValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    {prop.Name} = ERROR: {ex.Message}");
                    }
                }

                // If we found all four coordinates, construct the result
                if (x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
                {
                    var startCoord = new SCoordinate { dXLoc = x1.Value, dYLoc = y1.Value, dZLoc = 0 };
                    var endCoord = new SCoordinate { dXLoc = x2.Value, dYLoc = y2.Value, dZLoc = 0 };

                    Console.WriteLine($"  Edge {edgeIndex}: Constructed from properties - Start({startCoord.dXLoc:F3}, {startCoord.dYLoc:F3}) End({endCoord.dXLoc:F3}, {endCoord.dYLoc:F3})");
                    return (startCoord, endCoord);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Edge {edgeIndex}: Property search failed - {ex.Message}");
            }

            // Try Method 3: Look for Point objects
            try
            {
                var pointProperties = edgeType.GetProperties()
                    .Where(p => p.PropertyType.Name.ToLower().Contains("point"))
                    .ToArray();

                if (pointProperties.Length > 0)
                {
                    Console.WriteLine($"  Edge {edgeIndex}: Found {pointProperties.Length} point properties:");
                    foreach (var prop in pointProperties)
                    {
                        try
                        {
                            var value = prop.GetValue(edge);
                            Console.WriteLine($"    {prop.PropertyType.Name} {prop.Name} = {value}");

                            // Try to extract coordinates from point objects
                            if (value != null)
                            {
                                var pointType = value.GetType();
                                var coordMethod = pointType.GetMethod("GetCoordinate");
                                if (coordMethod != null)
                                {
                                    var coord = new SCoordinate();
                                    object[] coordParams = { coord };
                                    coordMethod.Invoke(value, coordParams);
                                    coord = (SCoordinate)coordParams[0];

                                    Console.WriteLine($"    -> Point coordinates: ({coord.dXLoc:F3}, {coord.dYLoc:F3}, {coord.dZLoc:F3})");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    {prop.Name} = ERROR: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Edge {edgeIndex}: Point property search failed - {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check if two points are equal within tolerance
        /// </summary>
        private bool ArePointsEqual(Point2D p1, Point2D p2, double tolerance = 1e-6)
        {
            return Math.Abs(p1.X - p2.X) < tolerance && Math.Abs(p1.Y - p2.Y) < tolerance;
        }
    }
}
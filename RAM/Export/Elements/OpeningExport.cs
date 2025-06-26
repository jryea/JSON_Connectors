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
    public class OpeningExport
    {
        private IModel _model;
        private string _lengthUnit;

        public OpeningExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public List<Opening> Export()
        {
            var openings = new List<Opening>();
            Console.WriteLine("Starting Opening export from RAM");

            try
            {
                IFloorTypes floorTypes = _model.GetFloorTypes();
                if (floorTypes == null || floorTypes.GetCount() == 0)
                {
                    Console.WriteLine("ERROR: No floor types found in RAM model");
                    return openings;
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

                    // Get slab openings for this floor type
                    ISlabOpenings slabOpenings = floorType.GetSlabOpenings();
                    if (slabOpenings == null || slabOpenings.GetCount() == 0)
                    {
                        Console.WriteLine($"No slab openings found for floor type {floorType.strLabel}");
                        continue;
                    }

                    Console.WriteLine($"Found {slabOpenings.GetCount()} slab opening(s) for floor type {floorType.strLabel}");

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

                        // Process each slab opening in this floor type
                        for (int openingIdx = 0; openingIdx < slabOpenings.GetCount(); openingIdx++)
                        {
                            ISlabOpening slabOpening = slabOpenings.GetAt(openingIdx);
                            if (slabOpening == null)
                            {
                                Console.WriteLine($"ERROR: Slab opening at index {openingIdx} is null");
                                continue;
                            }

                            Console.WriteLine($"Processing slab opening with UID: {slabOpening.lUID}");

                            // Extract opening points using multiple methods
                            var openingPoints = ExtractOpeningPoints(slabOpening, openingIdx);

                            if (openingPoints.Count < 3)
                            {
                                Console.WriteLine($"WARNING: Not enough points for opening from slab opening {openingIdx} (only {openingPoints.Count}, minimum 3 required)");
                                continue;
                            }

                            // Create opening object
                            var opening = new Opening
                            {
                                Id = IdGenerator.Generate(IdGenerator.Elements.OPENING),
                                LevelId = levelId,
                                Points = openingPoints
                            };

                            openings.Add(opening);
                            Console.WriteLine($"Successfully created opening {opening.Id} with {openingPoints.Count} points on level {levelId}");
                        }
                    }

                    if (!foundMatchingStory)
                    {
                        Console.WriteLine($"No stories found using floor type {floorType.strLabel}");
                    }
                }

                Console.WriteLine($"Successfully created {openings.Count} openings.");
                return openings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR exporting openings from RAM: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return openings;
            }
        }

        /// <summary>
        /// Extract opening points using multiple methods (vertices and edges)
        /// </summary>
        private List<Point2D> ExtractOpeningPoints(ISlabOpening slabOpening, int openingIdx)
        {
            Console.WriteLine($"\n=== EXTRACTING POINTS FOR OPENING {openingIdx} ===");

            // Method 1: Try GetOpeningVertices() (primary method)
            var vertexPoints = TryExtractPointsFromVertices(slabOpening, openingIdx);

            // Method 2: Try GetEdges() if vertices are insufficient
            var edgePoints = new List<Point2D>();
            if (vertexPoints.Count < 3)
            {
                Console.WriteLine($"Vertex points ({vertexPoints.Count}) insufficient, trying edges...");
                edgePoints = TryExtractPointsFromEdges(slabOpening, openingIdx);
            }

            // Choose the best result
            List<Point2D> finalPoints;
            if (edgePoints.Count > vertexPoints.Count)
            {
                Console.WriteLine($"Using edge points: {edgePoints.Count} points vs {vertexPoints.Count} vertex points");
                finalPoints = edgePoints;
            }
            else
            {
                Console.WriteLine($"Using vertex points: {vertexPoints.Count} points");
                finalPoints = vertexPoints;
            }

            Console.WriteLine($"=== FINAL RESULT: {finalPoints.Count} POINTS ===\n");
            return finalPoints;
        }

        /// <summary>
        /// Extract points using GetOpeningVertices() method
        /// </summary>
        private List<Point2D> TryExtractPointsFromVertices(ISlabOpening slabOpening, int openingIdx)
        {
            var openingPoints = new List<Point2D>();

            try
            {
                Console.WriteLine($"=== METHOD 1: GETVERTEXCOORDINATES() ===");

                // Get opening vertex points
                IPoints vertexPoints = slabOpening.GetOpeningVertices();

                int reportedPointCount = vertexPoints?.GetCount() ?? 0;
                Console.WriteLine($"RAM reports {reportedPointCount} vertex points for opening {openingIdx}");

                if (vertexPoints == null || vertexPoints.GetCount() < 3)
                {
                    Console.WriteLine($"Insufficient vertex points: {reportedPointCount}");
                    return openingPoints;
                }

                // Extract points with debugging
                int nullPointCount = 0;
                int invalidCoordCount = 0;
                int validPointCount = 0;
                int exceptionCount = 0;

                for (int ptIdx = 0; ptIdx < vertexPoints.GetCount(); ptIdx++)
                {
                    try
                    {
                        IPoint vertexPoint = vertexPoints.GetAt(ptIdx);

                        if (vertexPoint == null)
                        {
                            Console.WriteLine($"  Point {ptIdx}: NULL POINT - skipped");
                            nullPointCount++;
                            continue;
                        }

                        // Get the coordinates
                        SCoordinate coord = new SCoordinate();
                        vertexPoint.GetCoordinate(ref coord);

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

                        openingPoints.Add(point);
                        validPointCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Point {ptIdx}: EXCEPTION - {ex.Message}");
                        exceptionCount++;
                        continue;
                    }
                }

                Console.WriteLine($"Vertex extraction summary: {reportedPointCount} reported, {validPointCount} valid, {nullPointCount} null, {invalidCoordCount} invalid, {exceptionCount} exceptions");

                if (openingPoints.Count != reportedPointCount)
                {
                    int totalLost = nullPointCount + invalidCoordCount + exceptionCount;
                    Console.WriteLine($"*** MISMATCH: Expected {reportedPointCount}, got {openingPoints.Count} (lost {totalLost}) ***");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Vertex point extraction failed: {ex.Message}");
            }

            return openingPoints;
        }

        /// <summary>
        /// Extract points from slab opening edges (alternative method)
        /// </summary>
        private List<Point2D> TryExtractPointsFromEdges(ISlabOpening slabOpening, int openingIdx)
        {
            var edgePoints = new List<Point2D>();

            try
            {
                Console.WriteLine($"=== METHOD 2: SLAB OPENING EDGES ===");

                // Get edges for this slab opening
                ISlabEdges slabEdges = slabOpening.GetEdges();

                if (slabEdges == null)
                {
                    Console.WriteLine("No slab edges found for opening");
                    return edgePoints;
                }

                int edgeCount = slabEdges.GetCount();
                Console.WriteLine($"Found {edgeCount} slab edges for opening");

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
                        // Try to extract edge coordinates
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

            // Try Method 1: Look for any method with "Coordinate" in the name
            try
            {
                var coordinateMethods = edgeType.GetMethods()
                    .Where(m => m.Name.ToLower().Contains("coordinate"))
                    .ToArray();

                Console.WriteLine($"  Edge {edgeIndex}: Found {coordinateMethods.Length} coordinate methods:");
                foreach (var method in coordinateMethods)
                {
                    var paramStr = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                    Console.WriteLine($"    {method.ReturnType.Name} {method.Name}({paramStr})");

                    // Try to call methods that look promising
                    if (method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType == typeof(SCoordinate).MakeByRefType())
                    {
                        try
                        {
                            var coord = new SCoordinate();
                            object[] parameters = { coord };
                            method.Invoke(edge, parameters);
                            coord = (SCoordinate)parameters[0];

                            Console.WriteLine($"    -> Successfully called {method.Name}: ({coord.dXLoc:F3}, {coord.dYLoc:F3}, {coord.dZLoc:F3})");

                            // If this looks like a start/end method, collect both
                            if (method.Name.ToLower().Contains("start"))
                            {
                                // Look for corresponding end method
                                var endMethodName = method.Name.Replace("Start", "End").Replace("start", "end");
                                var endMethod = edgeType.GetMethod(endMethodName);
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
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    -> Failed to call {method.Name}: {ex.Message}");
                        }
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
                        Console.WriteLine($"    {prop.PropertyType.Name} {prop.Name} = {value}");

                        // Try to map to start/end coordinates
                        if (prop.PropertyType == typeof(double))
                        {
                            double doubleValue = (double)value;
                            string propName = prop.Name.ToLower();

                            if (propName.Contains("x1") || (propName.Contains("start") && propName.Contains("x")))
                                x1 = doubleValue;
                            else if (propName.Contains("y1") || (propName.Contains("start") && propName.Contains("y")))
                                y1 = doubleValue;
                            else if (propName.Contains("x2") || (propName.Contains("end") && propName.Contains("x")))
                                x2 = doubleValue;
                            else if (propName.Contains("y2") || (propName.Contains("end") && propName.Contains("y")))
                                y2 = doubleValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    {prop.Name} = ERROR: {ex.Message}");
                    }
                }

                // If we found coordinates, construct the result
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
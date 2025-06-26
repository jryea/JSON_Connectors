using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;

namespace ETABS.Import.Elements
{
    // Converts point coordinates for the E2K file format.
    public class PointCoordinatesImport
    {
        // Singleton instance of Point2DComparer to ensure consistent comparisons
        private readonly Point2DComparer _pointComparer;

        // Dictionary to store point IDs for reference by other converters
        private Dictionary<Point2D, string> _pointMapping;

        // Counter for generating point IDs
        private int _pointCounter;

        // Debug counters
        private int _pointsCreated = 0;
        private int _pointsReused = 0;

        // Initializes a new instance of the PointCoordinatesToETABS class.
        public PointCoordinatesImport()
        {
            _pointComparer = Point2DComparer.Instance;
            _pointMapping = new Dictionary<Point2D, string>(_pointComparer);
            _pointCounter = 1;
        }

        // Gets the point mapping dictionary for use by other converters.
        public Dictionary<Point2D, string> PointMapping => _pointMapping;

        // Gets or creates a point ID for the specified point.
        // This is the core method that ensures point deduplication.
        public string GetOrCreatePointId(Point2D point)
        {
            if (point == null)
                return "0";

            // Normalize to nearest 0.25 inch
            Point2D normalizedPoint = new Point2D(
                Math.Round(point.X * 4) / 4,
                Math.Round(point.Y * 4) / 4
            );

            string coordKey = $"({normalizedPoint.X:F2},{normalizedPoint.Y:F2})";

            if (_pointMapping.TryGetValue(normalizedPoint, out string existingId))
            {
                _pointsReused++;
                Console.WriteLine($"REUSED Point {coordKey} - ID: {existingId}");
                return existingId;
            }

            string pointId = _pointCounter.ToString();
            _pointMapping[normalizedPoint] = pointId;
            _pointCounter++;
            _pointsCreated++;

            Console.WriteLine($"CREATED Point {coordKey} - ID: {pointId}");

            return pointId;
        }

        // Converts a collection of structural elements to E2K format text for point coordinates.
        public string ConvertToE2K(ElementContainer elements, ModelLayoutContainer layout)
        {
            StringBuilder sb = new StringBuilder();

            // Reset for new conversion
            _pointMapping.Clear();
            _pointCounter = 1;
            _pointsCreated = 0;
            _pointsReused = 0;

            Console.WriteLine("PointMapping cleared and counter reset");

            // E2K Point Coordinates Header
            sb.AppendLine("$ POINT COORDINATES");

            // Process all elements to collect points
            Console.WriteLine($"Collecting points from {elements.Beams?.Count ?? 0} beams");
            CollectPoints(elements, layout);

            // Check for duplicates after collection
            CheckForDuplicates();

            // Write out all unique points
            foreach (var entry in _pointMapping)
            {
                string pointLine = FormatPointLine(entry.Value, entry.Key);
                sb.AppendLine(pointLine);
            }

            Console.WriteLine($"Points summary: Created {_pointsCreated}, Reused {_pointsReused}, Total {_pointMapping.Count}");

            return sb.ToString();
        }

        // Check for any duplicate points in the mapping
        private void CheckForDuplicates()
        {
            var coordCheck = new Dictionary<string, List<KeyValuePair<Point2D, string>>>();

            foreach (var entry in _pointMapping)
            {
                string key = $"{entry.Key.X:F6},{entry.Key.Y:F6}";
                if (!coordCheck.TryGetValue(key, out var pairs))
                {
                    pairs = new List<KeyValuePair<Point2D, string>>();
                    coordCheck[key] = pairs;
                }
                pairs.Add(entry);
            }

            int duplicatesFound = 0;

            foreach (var entry in coordCheck)
            {
                if (entry.Value.Count > 1)
                {
                    duplicatesFound++;
                    Console.WriteLine($"DUPLICATE IN MAPPING: {entry.Key} has {entry.Value.Count} points:");
                    foreach (var pair in entry.Value)
                    {
                        Console.WriteLine($"  Point ID {pair.Value}: ({pair.Key.X:F12},{pair.Key.Y:F12})");
                    }
                }
            }

            if (duplicatesFound == 0)
            {
                Console.WriteLine("NO DUPLICATES FOUND - All points are unique!");
            }
            else
            {
                Console.WriteLine($"FOUND {duplicatesFound} SETS OF DUPLICATE COORDINATES");
            }
        }

        // Collects all points from the model.
        private void CollectPoints(ElementContainer elements, ModelLayoutContainer layout)
        {
            // Process Walls
            if (elements.Walls != null)
            {
                Console.WriteLine($"Processing {elements.Walls.Count} walls");
                foreach (var wall in elements.Walls)
                {
                    if (wall.Points != null)
                    {
                        Console.WriteLine($"Wall {wall.Id} has {wall.Points.Count} points");
                        foreach (var point in wall.Points)
                        {
                            if (point != null)
                                GetOrCreatePointId(point);
                        }
                    }
                }
            }

            // Process Floors
            if (elements.Floors != null)
            {
                Console.WriteLine($"Processing {elements.Floors.Count} floors");
                foreach (var floor in elements.Floors)
                {
                    if (floor.Points != null)
                    {
                        Console.WriteLine($"Floor {floor.Id} has {floor.Points.Count} points");
                        foreach (var point in floor.Points)
                        {
                            if (point != null)
                                GetOrCreatePointId(point);
                        }
                    }
                }
            }

            // Process Openings  
            if (elements.Openings != null)
            {
                Console.WriteLine($"Processing {elements.Openings.Count} openings");
                foreach (var opening in elements.Openings)
                {
                    if (opening.Points != null)
                    {
                        Console.WriteLine($"Opening {opening.Id} has {opening.Points.Count} points");
                        foreach (var point in opening.Points)
                        {
                            if (point != null)
                                GetOrCreatePointId(point);
                        }
                    }
                }
            }

            // Process Beams
            if (elements.Beams != null)
            {
                Console.WriteLine($"Processing {elements.Beams.Count} beams");
                foreach (var beam in elements.Beams)
                {
                    if (beam.StartPoint != null)
                    {
                        Console.WriteLine($"Beam {beam.Id} StartPoint: ({beam.StartPoint.X:F2},{beam.StartPoint.Y:F2})");
                        GetOrCreatePointId(beam.StartPoint);
                    }
                    if (beam.EndPoint != null)
                    {
                        Console.WriteLine($"Beam {beam.Id} EndPoint: ({beam.EndPoint.X:F2},{beam.EndPoint.Y:F2})");
                        GetOrCreatePointId(beam.EndPoint);
                    }
                }
            }

            // Process Columns
            if (elements.Columns != null)
            {
                Console.WriteLine($"Processing {elements.Columns.Count} columns");
                foreach (var column in elements.Columns)
                {
                    if (column.StartPoint != null)
                    {
                        Console.WriteLine($"Column {column.Id} StartPoint: ({column.StartPoint.X:F2},{column.StartPoint.Y:F2})");
                        GetOrCreatePointId(column.StartPoint);
                    }
                    if (column.EndPoint != null)
                    {
                        Console.WriteLine($"Column {column.Id} EndPoint: ({column.EndPoint.X:F2},{column.EndPoint.Y:F2})");
                        GetOrCreatePointId(column.EndPoint);
                    }
                }
            }

            // Process Braces
            if (elements.Braces != null)
            {
                Console.WriteLine($"Processing {elements.Braces.Count} braces");
                foreach (var brace in elements.Braces)
                {
                    if (brace.StartPoint != null)
                    {
                        Console.WriteLine($"Brace {brace.Id} StartPoint: ({brace.StartPoint.X:F2},{brace.StartPoint.Y:F2})");
                        GetOrCreatePointId(brace.StartPoint);
                    }
                    if (brace.EndPoint != null)
                    {
                        Console.WriteLine($"Brace {brace.Id} EndPoint: ({brace.EndPoint.X:F2},{brace.EndPoint.Y:F2})");
                        GetOrCreatePointId(brace.EndPoint);
                    }
                }
            }
        }

        // Formats a point for the E2K file.
        private string FormatPointLine(string name, Point2D point)
        {
            // Format: POINT  "1"  -6843.36  -821.33
            return $"  POINT  \"{name}\"  {point.X:F2}  {point.Y:F2}";
        }
    }

    // Custom comparer for Point2D objects to ensure proper equality checking.
    // Implemented as a singleton to ensure consistent comparison throughout the application.
    public class Point2DComparer : IEqualityComparer<Point2D>
    {
        // Consistent tolerance value used across the application
        private const double Tolerance = 0.25;

        // Singleton instance
        private static readonly Point2DComparer _instance = new Point2DComparer();

        // Gets the singleton instance of the Point2DComparer.
        public static Point2DComparer Instance => _instance;

        // Private constructor to enforce singleton pattern
        private Point2DComparer() { }

        // Compares two points for equality within the tolerance.
        public bool Equals(Point2D x, Point2D y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;

            // Round to consistent precision before comparing
            double x1 = Math.Round(x.X, 6);
            double y1 = Math.Round(x.Y, 6);
            double x2 = Math.Round(y.X, 6);
            double y2 = Math.Round(y.Y, 6);

            return Math.Abs(x1 - x2) < Tolerance && Math.Abs(y1 - y2) < Tolerance;
        }

        // Gets a hash code for a point that's consistent with Equals.
        public int GetHashCode(Point2D obj)
        {
            if (obj == null)
                return 0;

            // Round to fixed precision for consistent hash codes
            double roundedX = Math.Round(obj.X, 6);
            double roundedY = Math.Round(obj.Y, 6);

            unchecked
            {
                int hash = 17;
                hash = hash * 23 + roundedX.GetHashCode();
                hash = hash * 23 + roundedY.GetHashCode();
                return hash;
            }
        }

        // Creates a normalized coordinate key for a point.
        public static string GetCoordinateKey(Point2D point)
        {
            if (point == null)
                return string.Empty;

            return $"{Math.Round(point.X, 6)},{Math.Round(point.Y, 6)}";
        }

        // Creates a normalized coordinate key for a line between two points.
        public static string GetLineCoordinateKey(Point2D start, Point2D end)
        {
            if (start == null || end == null)
                return string.Empty;

            double x1 = Math.Round(start.X, 6);
            double y1 = Math.Round(start.Y, 6);
            double x2 = Math.Round(end.X, 6);
            double y2 = Math.Round(end.Y, 6);

            // Ensure consistent ordering (smaller X or Y first)
            if ((Math.Abs(x2 - x1) > Math.Abs(y2 - y1) && x2 < x1) ||
                (Math.Abs(y2 - y1) >= Math.Abs(x2 - x1) && y2 < y1))
            {
                double tempX = x1;
                double tempY = y1;
                x1 = x2;
                y1 = y2;
                x2 = tempX;
                y2 = tempY;
            }

            // Format with consistent precision
            return $"{x1},{y1}_{x2},{y2}";
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;

namespace ETABS.ToETABS.Elements
{
    /// <summary>
    /// Converts point coordinates for the E2K file format.
    /// Acts as the single source of truth for all point IDs in the model.
    /// </summary>
    public class PointCoordinatesToETABS
    {
        // Singleton instance of Point2DComparer to ensure consistent comparisons
        private readonly Point2DComparer _pointComparer;

        // Dictionary to store point IDs for reference by other converters
        private Dictionary<Point2D, string> _pointMapping;

        // Counter for generating point IDs
        private int _pointCounter;

        /// <summary>
        /// Initializes a new instance of the PointCoordinatesToETABS class.
        /// </summary>
        public PointCoordinatesToETABS()
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

            // Create a normalized point with rounded coordinates to ensure consistency
            Point2D normalizedPoint = new Point2D(
                Math.Round(point.X, 6),
                Math.Round(point.Y, 6)
            );

            // Try to find the point using the dictionary's built-in lookup which uses the comparer
            if (_pointMapping.TryGetValue(normalizedPoint, out string existingId))
            {
                return existingId;
            }

            // If not found, create a new point ID
            string pointId = _pointCounter.ToString();
            _pointMapping[normalizedPoint] = pointId;
            _pointCounter++;

            return pointId;
        }

        /// <summary>
        /// Converts a collection of structural elements to E2K format text for point coordinates.
        /// </summary>
        /// <param name="elements">The collection of structural elements.</param>
        /// <param name="layout">The model layout container.</param>
        /// <returns>The E2K format text for point coordinates.</returns>
        public string ConvertToE2K(ElementContainer elements, ModelLayoutContainer layout)
        {
            StringBuilder sb = new StringBuilder();

            // Reset for new conversion
            _pointMapping.Clear();
            _pointCounter = 1;

            // E2K Point Coordinates Header
            sb.AppendLine("$ POINT COORDINATES");

            // Process all elements to collect points
            CollectPoints(elements, layout);

            // Write out all unique points
            foreach (var entry in _pointMapping)
            {
                string pointLine = FormatPointLine(entry.Value, entry.Key);
                sb.AppendLine(pointLine);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Collects all points from the model.
        /// </summary>
        /// <param name="elements">The collection of structural elements.</param>
        /// <param name="layout">The model layout container.</param>
        private void CollectPoints(ElementContainer elements, ModelLayoutContainer layout)
        {
            // Process Walls
            if (elements.Walls != null)
            {
                foreach (var wall in elements.Walls)
                {
                    if (wall.Points != null)
                    {
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
                foreach (var floor in elements.Floors)
                {
                    if (floor.Points != null)
                    {
                        foreach (var point in floor.Points)
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
                foreach (var beam in elements.Beams)
                {
                    if (beam.StartPoint != null)
                        GetOrCreatePointId(beam.StartPoint);
                    if (beam.EndPoint != null)
                        GetOrCreatePointId(beam.EndPoint);
                }
            }

            // Process Columns
            if (elements.Columns != null)
            {
                foreach (var column in elements.Columns)
                {
                    if (column.StartPoint != null)
                        GetOrCreatePointId(column.StartPoint);
                    if (column.EndPoint != null)
                        GetOrCreatePointId(column.EndPoint);
                }
            }

            // Process Braces
            if (elements.Braces != null)
            {
                foreach (var brace in elements.Braces)
                {
                    if (brace.StartPoint != null)
                        GetOrCreatePointId(brace.StartPoint);
                    if (brace.EndPoint != null)
                        GetOrCreatePointId(brace.EndPoint);
                }
            }

            // Process Grids
            if (layout?.Grids != null)
            {
                foreach (var grid in layout.Grids)
                {
                    if (grid.StartPoint != null)
                    {
                        var point = new Point2D(grid.StartPoint.X, grid.StartPoint.Y);
                        GetOrCreatePointId(point);
                    }
                    if (grid.EndPoint != null)
                    {
                        var point = new Point2D(grid.EndPoint.X, grid.EndPoint.Y);
                        GetOrCreatePointId(point);
                    }
                }
            }
        }

        /// <summary>
        /// Formats a point for the E2K file.
        /// </summary>
        /// <param name="name">The point ID.</param>
        /// <param name="point">The point coordinates.</param>
        /// <returns>The formatted point line for E2K.</returns>
        private string FormatPointLine(string name, Point2D point)
        {
            // Format: POINT  "1"  -6843.36  -821.33
            return $"  POINT  \"{name}\"  {point.X:F2}  {point.Y:F2}";
        }
    }

    /// <summary>
    /// Custom comparer for Point2D objects to ensure proper equality checking.
    /// Implemented as a singleton to ensure consistent comparison throughout the application.
    /// </summary>
    public class Point2DComparer : IEqualityComparer<Point2D>
    {
        // Consistent tolerance value used across the application
        private const double Tolerance = 1e-6;

        // Singleton instance
        private static readonly Point2DComparer _instance = new Point2DComparer();

        /// <summary>
        /// Gets the singleton instance of the Point2DComparer.
        /// </summary>
        public static Point2DComparer Instance => _instance;

        // Private constructor to enforce singleton pattern
        private Point2DComparer() { }

        /// <summary>
        /// Compares two points for equality within the tolerance.
        /// </summary>
        /// <param name="x">The first point.</param>
        /// <param name="y">The second point.</param>
        /// <returns>True if the points are equal within the tolerance, false otherwise.</returns>
        public bool Equals(Point2D x, Point2D y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;

            return Math.Abs(x.X - y.X) < Tolerance && Math.Abs(x.Y - y.Y) < Tolerance;
        }

        /// <summary>
        /// Gets a hash code for a point that's consistent with Equals.
        /// </summary>
        /// <param name="obj">The point.</param>
        /// <returns>A hash code for the point.</returns>
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

            // Normalize the points to ensure consistent keys
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
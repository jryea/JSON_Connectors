using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Geometry;

namespace Core.Utilities
{
    // Provides methods to detect and remove duplicate geometry elements in structural models
    public static class DuplicateGeometryHandler
    {
        // Tolerance for coordinate comparison
        private const double CoordinateTolerance = 1e-6;

        // Removes duplicate beams from a collection
        public static List<Beam> RemoveDuplicateBeams(IEnumerable<Beam> beams)
        {
            if (beams == null)
                return new List<Beam>();

            var uniqueBeams = new List<Beam>();
            var processedKeys = new HashSet<string>();

            foreach (var beam in beams)
            {
                // Skip invalid beams
                if (beam == null || beam.StartPoint == null || beam.EndPoint == null)
                    continue;

                // Create a geometric key for this beam (normalize direction)
                string beamKey = GetBeamGeometricKey(beam);

                // If we haven't seen this beam before, add it
                if (!processedKeys.Contains(beamKey))
                {
                    processedKeys.Add(beamKey);
                    uniqueBeams.Add(beam);
                }
            }

            return uniqueBeams;
        }

        // Removes duplicate columns from a collection
        public static List<Column> RemoveDuplicateColumns(IEnumerable<Column> columns)
        {
            if (columns == null)
                return new List<Column>();

            var uniqueColumns = new List<Column>();
            var processedKeys = new HashSet<string>();

            foreach (var column in columns)
            {
                // Skip invalid columns
                if (column == null || column.StartPoint == null)
                    continue;

                // Create a geometric key for this column
                string columnKey = GetColumnGeometricKey(column);

                // If we haven't seen this column before, add it
                if (!processedKeys.Contains(columnKey))
                {
                    processedKeys.Add(columnKey);
                    uniqueColumns.Add(column);
                }
            }

            return uniqueColumns;
        }

        // Removes duplicate walls from a collection
        public static List<Wall> RemoveDuplicateWalls(IEnumerable<Wall> walls)
        {
            if (walls == null)
                return new List<Wall>();

            var uniqueWalls = new List<Wall>();
            var processedKeys = new HashSet<string>();

            foreach (var wall in walls)
            {
                // Skip invalid walls
                if (wall == null || wall.Points == null || wall.Points.Count < 2)
                    continue;

                // Create a geometric key for this wall
                string wallKey = GetWallGeometricKey(wall);

                // If we haven't seen this wall before, add it
                if (!processedKeys.Contains(wallKey))
                {
                    processedKeys.Add(wallKey);
                    uniqueWalls.Add(wall);
                }
            }

            return uniqueWalls;
        }

        // Generates a unique geometric key for a beam
        private static string GetBeamGeometricKey(Beam beam)
        {
            // For beams, we want to normalize the direction (A->B is same as B->A)
            double x1 = beam.StartPoint.X;
            double y1 = beam.StartPoint.Y;
            double x2 = beam.EndPoint.X;
            double y2 = beam.EndPoint.Y;

            // Ensure consistent direction (smaller X or Y first)
            if ((Math.Abs(x2 - x1) > Math.Abs(y2 - y1) && x2 < x1) ||
                (Math.Abs(y2 - y1) >= Math.Abs(x2 - x1) && y2 < y1))
            {
                // Swap points
                double tempX = x1;
                double tempY = y1;
                x1 = x2;
                y1 = y2;
                x2 = tempX;
                y2 = tempY;
            }

            // Format with consistent precision
            return $"{Math.Round(x1, 6)},{Math.Round(y1, 6)}_" +
                   $"{Math.Round(x2, 6)},{Math.Round(y2, 6)}_" +
                   $"{beam.LevelId ?? ""}";
        }

        // Generates a unique geometric key for a column
        private static string GetColumnGeometricKey(Column column)
        {
            // For columns, location (StartPoint) and levels are the key factors
            return $"{Math.Round(column.StartPoint.X, 6)},{Math.Round(column.StartPoint.Y, 6)}_" +
                   $"{column.BaseLevelId ?? ""}_" +
                   $"{column.TopLevelId ?? ""}";
        }

        // Generates a unique geometric key for a wall
        private static string GetWallGeometricKey(Wall wall)
        {
            // For walls with only two points, we normalize direction
            if (wall.Points.Count == 2)
            {
                double x1 = wall.Points[0].X;
                double y1 = wall.Points[0].Y;
                double x2 = wall.Points[1].X;
                double y2 = wall.Points[1].Y;

                // Ensure consistent direction (smaller X or Y first)
                if ((Math.Abs(x2 - x1) > Math.Abs(y2 - y1) && x2 < x1) ||
                    (Math.Abs(y2 - y1) >= Math.Abs(x2 - x1) && y2 < y1))
                {
                    // Swap points
                    return $"{Math.Round(x2, 6)},{Math.Round(y2, 6)}_" +
                           $"{Math.Round(x1, 6)},{Math.Round(y1, 6)}_" +
                           $"{wall.BaseLevelId ?? ""}_" +
                           $"{wall.TopLevelId ?? ""}";
                }

                return $"{Math.Round(x1, 6)},{Math.Round(y1, 6)}_" +
                       $"{Math.Round(x2, 6)},{Math.Round(y2, 6)}_" +
                       $"{wall.BaseLevelId ?? ""}_" +
                       $"{wall.TopLevelId ?? ""}";
            }

            // For multi-point walls, we need to create a signature of all points
            var pointStrings = new List<string>();
            foreach (var point in wall.Points)
            {
                pointStrings.Add($"{Math.Round(point.X, 6)},{Math.Round(point.Y, 6)}");
            }

            // Sort points for consistent ordering
            pointStrings.Sort();

            return string.Join("_", pointStrings) +
                   $"_{wall.BaseLevelId ?? ""}_" +
                   $"{wall.TopLevelId ?? ""}";
        }

        // Determines if two points are equal within tolerance
        public static bool ArePointsEqual(Point2D p1, Point2D p2)
        {
            if (p1 == null || p2 == null)
                return false;

            return Math.Abs(p1.X - p2.X) < CoordinateTolerance &&
                   Math.Abs(p1.Y - p2.Y) < CoordinateTolerance;
        }

        // Removes duplicate elements from a model
        public static void RemoveDuplicateElements(BaseModel model)
        {
            if (model?.Elements == null)
                return;

            model.Elements.Beams = RemoveDuplicateBeams(model.Elements.Beams);
            model.Elements.Columns = RemoveDuplicateColumns(model.Elements.Columns);
            model.Elements.Walls = RemoveDuplicateWalls(model.Elements.Walls);
            // Add handling for other element types as needed
        }
    }
}
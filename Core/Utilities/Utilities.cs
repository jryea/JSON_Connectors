using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;

namespace Core.Utilities
{
    public static class Utilities
    {
        // Tolerance for point comparisons
        private const double PointTolerance = 1e-6;

        public static bool AreLinePointsVertical(Point3D startPt, Point3D endPt)
        {
            return Math.Abs(endPt.Y - startPt.Y) > Math.Abs(endPt.X - startPt.X);
        }

        public static bool AreLinePointsVertical(Point2D startPt, Point2D endPt)
        {
            return Math.Abs(endPt.Y - startPt.Y) > Math.Abs(endPt.X - startPt.X);
        }

        public static WallProperties FindWallProperties(IEnumerable<WallProperties> properties, string propertyId)
        {
            foreach (var prop in properties)
            {
                if (prop.Id == propertyId)
                    return prop;
            }
            return null;
        }

        public static FloorProperties FindFloorProperties(IEnumerable<FloorProperties> properties, string propertyId)
        {
            foreach (var prop in properties)
            {
                if (prop.Id == propertyId)
                    return prop;
            }
            return null;
        }

        public static Level FindLevel(IEnumerable<Level> levels, string levelId)
        {
            foreach (var level in levels)
            {
                if (level.Id == levelId)
                    return level;
            }
            return null;
        }

        // Add this method to the Utilities class with debugging capabilities
        public static string GetPointId(Point2D point, Dictionary<Point2D, string> pointMapping, StringBuilder debugLog = null)
        {
            if (point == null || pointMapping == null || pointMapping.Count == 0)
            {
                if (debugLog != null)
                    debugLog.AppendLine("$ GetPointId: Null point or empty mapping, returning '0'");
                return "0";
            }

            // Log the target point we're looking for
            if (debugLog != null)
                debugLog.AppendLine($"$ GetPointId: Looking for point X={point.X}, Y={point.Y}");

            // Check for exact match (within tolerance)
            foreach (var entry in pointMapping)
            {
                // Check if the points match within tolerance
                bool pointsMatch = ArePointsEqual(entry.Key, point);

                if (debugLog != null)
                {
                    debugLog.AppendLine($"$ Comparing with mapped point X={entry.Key.X}, Y={entry.Key.Y}, ID={entry.Value}");
                    debugLog.AppendLine($"$ Distance: {Math.Sqrt(Math.Pow(entry.Key.X - point.X, 2) + Math.Pow(entry.Key.Y - point.Y, 2))}");
                    debugLog.AppendLine($"$ Points match within tolerance: {pointsMatch}");
                }

                if (pointsMatch)
                {
                    if (debugLog != null)
                        debugLog.AppendLine($"$ MATCH FOUND: Using point ID {entry.Value}");
                    return entry.Value;
                }
            }

            // If no exact match found, find closest point
            double minDistance = double.MaxValue;
            string closestPointId = "0";

            foreach (var entry in pointMapping)
            {
                double distance = Math.Sqrt(
                    Math.Pow(entry.Key.X - point.X, 2) +
                    Math.Pow(entry.Key.Y - point.Y, 2));

                if (debugLog != null)
                    debugLog.AppendLine($"$ Distance to point ID {entry.Value}: {distance}");

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPointId = entry.Value;
                }
            }

            // Use closest point if within reasonable tolerance
            if (minDistance < 0.1)
            {
                if (debugLog != null)
                    debugLog.AppendLine($"$ Using closest point ID {closestPointId} with distance {minDistance}");
                return closestPointId;
            }

            // Default to "0" if no match found
            if (debugLog != null)
                debugLog.AppendLine("$ No suitable match found, returning '0'");
            return "0";
        }

        // Checks if two points are equal within a small tolerance
        public static bool ArePointsEqual(Point2D p1, Point2D p2)
        {
            if (p1 == null || p2 == null)
                return false;

            return Math.Abs(p1.X - p2.X) < PointTolerance && Math.Abs(p1.Y - p2.Y) < PointTolerance;
        }
    }
}
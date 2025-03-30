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

        public static string GetPointId(Point2D point, Dictionary<Point2D, string> pointMapping)
        {
            // Check for exact match
            foreach (var entry in pointMapping)
            {
                if (Math.Abs(entry.Key.X - point.X) < 0.001 && Math.Abs(entry.Key.Y - point.Y) < 0.001)
                {
                    return entry.Value;
                }
            }

            // If no exact match found, try to find the closest point
            double minDistance = double.MaxValue;
            string closestPointId = "0";

            foreach (var entry in pointMapping)
            {
                double distance = Math.Sqrt(
                    Math.Pow(entry.Key.X - point.X, 2) +
                    Math.Pow(entry.Key.Y - point.Y, 2));

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPointId = entry.Value;
                }
            }

            // Only use closest point if it's within a reasonable tolerance
            if (minDistance < 0.1)
            {
                return closestPointId;
            }

            // Default to "0" if no match found
            return "0";
        }

        // Checks if two points are equal within a small tolerance
        public static bool ArePointsEqual(Point2D p1, Point2D p2)
        {
            const double tolerance = 0.001;
            return Math.Abs(p1.X - p2.X) < tolerance && Math.Abs(p1.Y - p2.Y) < tolerance;
        }
    }
}

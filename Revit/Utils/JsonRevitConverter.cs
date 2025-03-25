using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Core.Models.Elements;

namespace Revit.Utils
{
    /// <summary>
    /// Utility methods for converting between JSON model and Revit types
    /// </summary>
    public static class RevitTypeHelper
    {
        /// <summary>
        /// Converts a JSON 3D point to Revit XYZ
        /// </summary>
        public static XYZ ConvertToRevitCoordinates(Point3D point)
        {
            // Convert from the JSON units (inches) to Revit's internal units (feet)
            double x = point.X / 12.0;
            double y = point.Y / 12.0;
            double z = point.Z / 12.0;

            return new XYZ(x, y, z);
        }

        /// <summary>
        /// Converts a JSON 2D point to Revit XYZ
        /// </summary>
        public static XYZ ConvertToRevitCoordinates(Point2D point, double z = 0)
        {
            // Convert from the JSON units (inches) to Revit's internal units (feet)
            double x = point.X / 12.0;
            double y = point.Y / 12.0;

            return new XYZ(x, y, z);
        }

        /// <summary>
        /// Creates a Revit curve from a list of JSON points
        /// </summary>
        public static Curve CreateRevitCurve(List<Point2D> points, double z = 0)
        {
            if (points.Count < 2)
                throw new ArgumentException("A curve requires at least 2 points");

            if (points.Count == 2)
            {
                XYZ start = ConvertToRevitCoordinates(points[0], z);
                XYZ end = ConvertToRevitCoordinates(points[1], z);
                return Line.CreateBound(start, end);
            }
            else
            {
                // For more than 2 points, create a polyline
                XYZ[] xyzPoints = new XYZ[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    xyzPoints[i] = ConvertToRevitCoordinates(points[i], z);
                }

                return PolyLine.Create(xyzPoints).ToRevitType();
            }
        }

        /// <summary>
        /// Gets a Revit ElementId from a dictionary based on a JSON ID
        /// </summary>
        public static ElementId GetElementId(Dictionary<string, ElementId> idMap, string jsonId, string elementType)
        {
            if (idMap.TryGetValue(jsonId, out ElementId id))
                return id;

            throw new KeyNotFoundException($"Could not find {elementType} with ID {jsonId} in the model");
        }
    }
}
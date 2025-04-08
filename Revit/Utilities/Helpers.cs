using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using Core.Models.Elements;
using Core.Models.Geometry;
using Autodesk.Revit.DB;

namespace Revit.Utilities
{
    // Utility methods for converting between JSON model and Revit types
    internal static class Helpers
    {
        // Converts a JSON 3D point to Revit XYZ
        public static DB.XYZ ConvertToRevitCoordinates(Point3D point)
        {
            // Convert from the JSON units (inches) to Revit's internal units (feet)
            double x = point.X / 12.0;
            double y = point.Y / 12.0;
            double z = point.Z / 12.0;

            return new DB.XYZ(x, y, z);
        }

        // Converts a JSON 2D point to Revit XYZ
        public static DB.XYZ ConvertToRevitCoordinates(Point2D point, double z = 0)
        {
            // Convert from the JSON units (inches) to Revit's internal units (feet)
            double x = point.X / 12.0;
            double y = point.Y / 12.0;

            return new DB.XYZ(x, y, z);
        }

        // Creates a Revit curve from a list of JSON points
        public static DB.Curve CreateRevitCurve(Point3D startPt, Point3D endPt, double z = 0)
        {
            if (startPt == null || endPt == null)
                throw new ArgumentNullException("Start and end points cannot be null"); 
            
           
            DB.XYZ start = ConvertToRevitCoordinates(startPt);
            DB.XYZ end = ConvertToRevitCoordinates(endPt);
            return DB.Line.CreateBound(start, end);
        }

        internal static ElementId GetElementId(Dictionary<string, ElementId> levelIdMap, string levelId)
        {
            throw new NotImplementedException();
        }
    }
}
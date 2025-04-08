using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;

namespace Revit.Utilities
{
    // Utility methods for converting between JSON model and Revit types
    internal static class Helpers
    {
        // Converts a JSON 3D point to Revit XYZ
        public static DB.XYZ ConvertToRevitCoordinates(CG.Point3D point)
        {
            // Convert from the JSON units (inches) to Revit's internal units (feet)
            double x = point.X / 12.0;
            double y = point.Y / 12.0;
            double z = point.Z / 12.0;

            return new DB.XYZ(x, y, z);
        }

        // Converts a JSON 2D point to Revit XYZ
        public static DB.XYZ ConvertToRevitCoordinates(CG.Point2D point, double z = 0)
        {
            // Convert from the JSON units (inches) to Revit's internal units (feet)
            double x = point.X / 12.0;
            double y = point.Y / 12.0;

            return new DB.XYZ(x, y, z);
        }

        // Creates a Revit curve from a list of JSON points
        public static DB.Curve CreateRevitCurve(CG.Point2D startPt, CG.Point2D endPt, double z = 0)
        {
            if (startPt == null || endPt == null)
                throw new ArgumentNullException("Start and end points cannot be null"); 
            
           
            DB.XYZ start = ConvertToRevitCoordinates(startPt);
            DB.XYZ end = ConvertToRevitCoordinates(endPt);
            return DB.Line.CreateBound(start, end);
        }

        internal static DB.ElementId GetElementId(Dictionary<string, DB.ElementId> levelIdMap, string levelId)
        {
            throw new NotImplementedException();
        }
    }
}
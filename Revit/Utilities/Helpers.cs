using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;
using Autodesk.Revit.DB;
using Core.Models.Metadata;
using System.Diagnostics;

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

        // Converts Revit XYZ to JSON 3D point
        public static CG.Point3D ConvertToJsonCoordinates(DB.XYZ point)
        {
            // Convert from Revit's internal units (feet) to JSON units (inches)
            double x = point.X * 12.0;
            double y = point.Y * 12.0;
            double z = point.Z * 12.0;

            return new CG.Point3D(x, y, z);
        }

        // Converts Revit XYZ to JSON 2D point
        public static CG.Point2D ConvertToJsonCoordinates2D(DB.XYZ point)
        {
            // Convert from Revit's internal units (feet) to JSON units (inches)
            double x = point.X * 12.0;
            double y = point.Y * 12.0;

            return new CG.Point2D(x, y);
        }

        // Add to Revit/Utilities/Helpers.cs
        public static Coordinates ExtractCoordinateSystem(Document doc)
        {
            Coordinates coords = new Coordinates();

            try
            {
                // Get project base point
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfCategory(BuiltInCategory.OST_ProjectBasePoint);
                Element projectBasePoint = collector.FirstElement();

                if (projectBasePoint != null)
                {
                    // Get the position
                    double x = projectBasePoint.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM)?.AsDouble() ?? 0.0;
                    double y = projectBasePoint.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM)?.AsDouble() ?? 0.0;
                    double z = projectBasePoint.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM)?.AsDouble() ?? 0.0;

                    // Convert to inches (Revit internal units are feet) and round to 2 decimal places
                    coords.ProjectBasePoint = new CG.Point3D(
                        Math.Round(x * 12.0, 2),
                        Math.Round(y * 12.0, 2),
                        Math.Round(z * 12.0, 2)
                    );

                    // Get angle to true north (in radians)
                    Parameter angleParam = projectBasePoint.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM);
                    if (angleParam != null)
                    {
                        coords.Rotation = Math.Round(angleParam.AsDouble(), 2);
                    }
                }

                // Get survey point
                collector = new FilteredElementCollector(doc);
                collector.OfCategory(BuiltInCategory.OST_SharedBasePoint);
                Element surveyPoint = collector.FirstElement();

                if (surveyPoint != null)
                {
                    // Get the position
                    double x = surveyPoint.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM)?.AsDouble() ?? 0.0;
                    double y = surveyPoint.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM)?.AsDouble() ?? 0.0;
                    double z = surveyPoint.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM)?.AsDouble() ?? 0.0;

                    // Convert to inches and round to 2 decimal places
                    coords.SurveyPoint = new CG.Point3D(
                        Math.Round(x * 12.0, 2),
                        Math.Round(y * 12.0, 2),
                        Math.Round(z * 12.0, 2)
                        );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting coordinate system: {ex.Message}");
            }

            return coords;
        }
    }
}
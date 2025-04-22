using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;
using Core.Models;
using Revit.Utilities;

namespace Revit.Export.Elements
{
    public class IsolatedFootingExport
    {
        private readonly DB.Document _doc;

        public IsolatedFootingExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<CE.IsolatedFooting> footings, BaseModel model)
        {
            int count = 0;

            // Get all isolated footings from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FamilyInstance> revitFootings = collector.OfClass(typeof(DB.FamilyInstance))
                .OfCategory(DB.BuiltInCategory.OST_StructuralFoundation)
                .Cast<DB.FamilyInstance>()
                .Where(f => IsIsolatedFooting(f))
                .ToList();

            // Create mappings
            Dictionary<DB.ElementId, string> levelIdMap = CreateLevelMapping(model);

            // Export each footing
            foreach (var revitFooting in revitFootings)
            {
                try
                {
                    // Get footing location
                    DB.LocationPoint location = revitFooting.Location as DB.LocationPoint;
                    if (location == null)
                        continue;

                    DB.XYZ point = location.Point;

                    // Create footing object
                    CE.IsolatedFooting footing = new CE.IsolatedFooting();

                    // Set level
                    DB.ElementId levelId = revitFooting.get_Parameter(DB.BuiltInParameter.FAMILY_LEVEL_PARAM).AsElementId();
                    if (levelIdMap.ContainsKey(levelId))
                        footing.LevelId = levelIdMap[levelId];

                    // Set footing location
                    footing.Point = new CG.Point3D(
                        point.X * 12.0,
                        point.Y * 12.0,
                        point.Z * 12.0); // Convert to inches

                    // Get footing dimensions
                    ExtractFootingDimensions(revitFooting, footing);

                    footings.Add(footing);
                    count++;
                    Debug.WriteLine($"Exported footing at ({point.X}, {point.Y}, {point.Z})");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting footing: {ex.Message}");
                    // Skip this footing and continue with the next one
                }
            }

            return count;
        }

        private bool IsIsolatedFooting(DB.FamilyInstance instance)
        {
            // Determine if the structural foundation is an isolated footing
            // by checking family/type name or parameters
            try
            {
                string familyName = instance.Symbol.FamilyName.ToUpper();
                string typeName = instance.Symbol.Name.ToUpper();

                // Check if it's a rectangular/square/isolated footing
                return familyName.Contains("FOOTING") &&
                      (typeName.Contains("SQUARE") ||
                       typeName.Contains("RECTANGLE") ||
                       typeName.Contains("ISOLATED") ||
                       !typeName.Contains("WALL") && !typeName.Contains("CONTINUOUS"));
            }
            catch
            {
                // Default to false if there's an error
                return false;
            }
        }

        private void ExtractFootingDimensions(DB.FamilyInstance footingInstance, CE.IsolatedFooting jsonFooting)
        {
            try
            {
                // Try to get dimensions from built-in parameters
                DB.Parameter lengthParam = footingInstance.Symbol.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH);
                DB.Parameter widthParam = footingInstance.Symbol.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH);

                if (lengthParam != null && lengthParam.HasValue)
                    jsonFooting.Length = lengthParam.AsDouble() * 12.0; // Convert to inches
                else
                    jsonFooting.Length = 36.0; // Default 3' in inches

                if (widthParam != null && widthParam.HasValue)
                    jsonFooting.Width = widthParam.AsDouble() * 12.0; // Convert to inches
                else
                    jsonFooting.Width = 36.0; // Default 3' in inches

                // Try to get thickness from type parameter
                DB.Parameter thicknessParam = footingInstance.Symbol.LookupParameter("Thickness");
                if (thicknessParam != null && thicknessParam.HasValue)
                {
                    jsonFooting.Thickness = thicknessParam.AsDouble() * 12.0; // Convert to inches
                }
                else
                {
                    // Try to get thickness from instance parameter
                    thicknessParam = footingInstance.LookupParameter("Thickness");
                    if (thicknessParam != null && thicknessParam.HasValue)
                        jsonFooting.Thickness = thicknessParam.AsDouble() * 12.0;
                    else
                        jsonFooting.Thickness = 12.0; // Default 1' in inches
                }

                // Try to extract dimensions from family type name
                string typeName = footingInstance.Symbol.Name;
                if (typeName.Contains("x") && !TryParseDimensionsFromTypeName(typeName, jsonFooting))
                {
                    Debug.WriteLine($"Could not parse dimensions from type name: {typeName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting footing dimensions: {ex.Message}");
                // Use default values for dimensions
                jsonFooting.Length = 36.0; // 3'
                jsonFooting.Width = 36.0;  // 3'
                jsonFooting.Thickness = 12.0; // 1'
            }
        }

        private bool TryParseDimensionsFromTypeName(string typeName, CE.IsolatedFooting jsonFooting)
        {
            try
            {
                // Handle IMEG standard format: "2'-0" x 2'-0" x 1'-0""
                string[] parts = typeName.Split(new[] { " x ", "x" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    // Try to parse each dimension
                    double length = ParseFootInchDimension(parts[0]);
                    double width = ParseFootInchDimension(parts[1]);
                    double thickness = ParseFootInchDimension(parts[2]);

                    if (length > 0 && width > 0 && thickness > 0)
                    {
                        jsonFooting.Length = length * 12.0; // Convert to inches
                        jsonFooting.Width = width * 12.0;
                        jsonFooting.Thickness = thickness * 12.0;
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private double ParseFootInchDimension(string dimension)
        {
            try
            {
                // Handle format like "2'-0""
                string cleanDimension = dimension.Trim().Replace("\"", "");
                string[] parts = cleanDimension.Split('\'');

                double feet = double.Parse(parts[0]);
                double inches = 0;

                if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                {
                    inches = double.Parse(parts[1]);
                }

                return feet + (inches / 12.0);
            }
            catch
            {
                return 0;
            }
        }

        private Dictionary<DB.ElementId, string> CreateLevelMapping(BaseModel model)
        {
            Dictionary<DB.ElementId, string> levelMap = new Dictionary<DB.ElementId, string>();

            // Get all levels from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Level> revitLevels = collector.OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .ToList();

            // Map each Revit level to the corresponding level in the model
            foreach (var revitLevel in revitLevels)
            {
                var modelLevel = model.ModelLayout.Levels.FirstOrDefault(l =>
                    l.Name == revitLevel.Name ||
                    Math.Abs(l.Elevation - (revitLevel.Elevation * 12.0)) < 0.1); // Convert feet to inches with small tolerance

                if (modelLevel != null)
                {
                    levelMap[revitLevel.Id] = modelLevel.Id;
                }
            }

            return levelMap;
        }
    }
}
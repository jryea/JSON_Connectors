using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;

namespace Revit.Export
{
    /// <summary>
    /// Simplified column exporter that exports ALL columns with proper segmentation
    /// No filtering logic - just clean export with consistent property references
    /// </summary>
    public class SimpleColumnExporter
    {
        private readonly ExportContext _context;

        public SimpleColumnExporter(ExportContext context)
        {
            _context = context;
        }

        public List<CE.Column> Export()
        {
            var columns = new List<CE.Column>();

            Debug.WriteLine("SimpleColumnExporter: Starting export");

            // Get all structural columns from Revit
            var collector = new FilteredElementCollector(_context.RevitDoc);
            var revitColumns = collector.OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilyInstance>()
                .ToList();

            Debug.WriteLine($"SimpleColumnExporter: Found {revitColumns.Count} columns in Revit");

            // Create frame properties mapping
            var framePropertiesMap = CreateFramePropertiesMapping();

            // Get ordered levels for segmentation
            var orderedLevels = _context.RevitLevels.Values
                .OrderBy(l => l.Elevation)
                .ToList();

            foreach (var revitColumn in revitColumns)
            {
                try
                {
                    var columnSegments = CreateColumnSegments(revitColumn, framePropertiesMap, orderedLevels);
                    columns.AddRange(columnSegments);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SimpleColumnExporter: Error processing column {revitColumn.Id}: {ex.Message}");
                }
            }

            Debug.WriteLine($"SimpleColumnExporter: Created {columns.Count} column segments");
            return columns;
        }

        private List<CE.Column> CreateColumnSegments(FamilyInstance revitColumn,
            Dictionary<ElementId, string> framePropertiesMap, List<Level> orderedLevels)
        {
            var segments = new List<CE.Column>();

            // Get column location
            var location = revitColumn.Location as LocationPoint;
            if (location == null) return segments;

            var point = location.Point;

            // Get column elevation range
            double bottomElevation = GetColumnBottomElevation(revitColumn);
            double topElevation = GetColumnTopElevation(revitColumn);

            // Find intersecting level elevations
            var intersectingElevations = FindIntersectingLevelElevations(bottomElevation, topElevation, orderedLevels);

            // Create segments between each pair of elevations
            for (int i = 0; i < intersectingElevations.Count - 1; i++)
            {
                double currentBottom = intersectingElevations[i];
                double currentTop = intersectingElevations[i + 1];

                var baseLevel = FindClosestLevel(currentBottom, orderedLevels);
                var topLevel = FindClosestLevel(currentTop, orderedLevels);

                if (baseLevel == null || topLevel == null || baseLevel.Id.Equals(topLevel.Id))
                    continue;

                var segment = CreateColumnSegment(revitColumn, point, baseLevel, topLevel, framePropertiesMap);
                if (segment != null)
                {
                    segments.Add(segment);
                }
            }

            return segments;
        }

        private CE.Column CreateColumnSegment(FamilyInstance revitColumn, XYZ point,
            Level baseLevel, Level topLevel, Dictionary<ElementId, string> framePropertiesMap)
        {
            var column = new CE.Column();

            // Set geometry
            column.StartPoint = new CG.Point2D(point.X * 12.0, point.Y * 12.0);
            column.EndPoint = column.StartPoint;

            // Set levels using context mapping
            column.BaseLevelId = _context.GetModelLevelId(baseLevel.Id);
            column.TopLevelId = _context.GetModelLevelId(topLevel.Id);

            // Set frame properties
            var typeId = revitColumn.GetTypeId();
            if (framePropertiesMap.ContainsKey(typeId))
            {
                column.FramePropertiesId = framePropertiesMap[typeId];
            }

            // Set other properties
            column.Orientation = GetColumnOrientation(revitColumn);
            column.IsLateral = IsColumnLateral(revitColumn);

            return column;
        }

        private double GetColumnBottomElevation(FamilyInstance column)
        {
            var baseLevelParam = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
            if (baseLevelParam == null) return 0.0;

            var baseLevelId = baseLevelParam.AsElementId();
            if (!_context.RevitLevels.ContainsKey(baseLevelId)) return 0.0;

            var baseLevel = _context.RevitLevels[baseLevelId];
            var baseOffset = GetParameterValue(column, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);

            return baseLevel.Elevation + baseOffset;
        }

        private double GetColumnTopElevation(FamilyInstance column)
        {
            var topLevelParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
            if (topLevelParam == null) return 0.0;

            var topLevelId = topLevelParam.AsElementId();
            if (!_context.RevitLevels.ContainsKey(topLevelId)) return 0.0;

            var topLevel = _context.RevitLevels[topLevelId];
            var topOffset = GetParameterValue(column, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);

            return topLevel.Elevation + topOffset;
        }

        private double GetParameterValue(Element element, BuiltInParameter paramId)
        {
            var param = element.get_Parameter(paramId);
            return param != null && param.HasValue ? param.AsDouble() : 0.0;
        }

        private List<double> FindIntersectingLevelElevations(double bottomElevation, double topElevation, List<Level> levels)
        {
            var intersections = new List<double> { bottomElevation };

            foreach (var level in levels)
            {
                if (level.Elevation > bottomElevation && level.Elevation < topElevation)
                {
                    intersections.Add(level.Elevation);
                }
            }

            intersections.Sort();
            return intersections;
        }

        private Level FindClosestLevel(double elevation, List<Level> levels)
        {
            return levels.OrderBy(l => Math.Abs(l.Elevation - elevation)).FirstOrDefault();
        }

        private double GetColumnOrientation(FamilyInstance column)
        {
            var handOrientation = column.HandOrientation;
            double x = handOrientation.X;
            double y = handOrientation.Y;

            if (Math.Abs(x) < 1e-6 && Math.Abs(y) < 1e-6)
                return 0.0;

            double angleRad = Math.Atan2(x, y);
            double angleDeg = angleRad * 180.0 / Math.PI;

            if (angleDeg < 0)
                angleDeg += 360.0;

            return angleDeg % 180.0;
        }

        private bool IsColumnLateral(FamilyInstance column)
        {
            try
            {
                string familyName = column.Symbol.FamilyName.ToUpper();
                string typeName = column.Symbol.Name.ToUpper();

                return familyName.Contains("MOMENT") ||
                       familyName.Contains("LATERAL") ||
                       typeName.Contains("MOMENT") ||
                       typeName.Contains("LATERAL");
            }
            catch
            {
                return false;
            }
        }

        private Dictionary<ElementId, string> CreateFramePropertiesMapping()
        {
            var propsMap = new Dictionary<ElementId, string>();

            // This would ideally reference the built frame properties from the model
            // For now, create a simple mapping based on symbol names
            var collector = new FilteredElementCollector(_context.RevitDoc);
            var symbols = collector.OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();

            foreach (var symbol in symbols)
            {
                // Create a consistent frame property ID based on symbol name
                string framePropertyId = $"FRP-{symbol.Name.Replace(" ", "")}";
                propsMap[symbol.Id] = framePropertyId;
            }

            return propsMap;
        }
    }
}
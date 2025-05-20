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
    public class ColumnExport
    {
        private readonly DB.Document _doc;

        public ColumnExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<CE.Column> columns, BaseModel model)
        {
            int count = 0;

            // Get all columns from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FamilyInstance> revitColumns = collector.OfClass(typeof(DB.FamilyInstance))
                .OfCategory(DB.BuiltInCategory.OST_StructuralColumns)
                .Cast<DB.FamilyInstance>()
                .ToList();

            // Create mappings
            Dictionary<DB.ElementId, string> levelIdMap = CreateLevelMapping(model);
            Dictionary<DB.ElementId, string> framePropertiesMap = CreateFramePropertiesMapping(model);

            // Get ordered list of levels
            List<DB.Level> orderedLevels = GetOrderedLevels();

            // Create a dictionary to quickly look up levels by elevation
            Dictionary<double, DB.Level> elevationToLevelMap = new Dictionary<double, DB.Level>();
            foreach (var level in orderedLevels)
            {
                elevationToLevelMap[level.Elevation] = level;
            }

            foreach (var revitColumn in revitColumns)
            {
                try
                {
                    // Get column location
                    DB.LocationPoint location = revitColumn.Location as DB.LocationPoint;
                    if (location == null)
                        continue;

                    DB.XYZ point = location.Point;

                    // Get frame properties ID
                    string framePropertiesId = null;
                    DB.ElementId typeId = revitColumn.GetTypeId();
                    if (framePropertiesMap.ContainsKey(typeId))
                        framePropertiesId = framePropertiesMap[typeId];

                    // Get column orientation
                    double orientation = GetColumnOrientation(revitColumn);

                    // Determine if column is part of lateral system
                    bool isLateral = IsColumnLateral(revitColumn);

                    // Calculate actual top and bottom elevations including offsets
                    double bottomElevation = GetColumnBottomElevation(revitColumn);
                    double topElevation = GetColumnTopElevation(revitColumn);

                    // Find levels that this column spans
                    List<double> intersectingLevelElevations = FindIntersectingLevelElevations(
                        bottomElevation, topElevation, orderedLevels);

                    // Make sure we include the top elevation if it doesn't exactly match a level
                    if (!intersectingLevelElevations.Contains(topElevation))
                    {
                        intersectingLevelElevations.Add(topElevation);
                        intersectingLevelElevations.Sort();
                    }

                    // Create column segments between each intersection
                    for (int i = 0; i < intersectingLevelElevations.Count - 1; i++)
                    {
                        double currentBottom = intersectingLevelElevations[i];
                        double currentTop = intersectingLevelElevations[i + 1];

                        // Find closest levels to these elevations
                        DB.Level baseLevel = FindClosestLevel(currentBottom, orderedLevels);
                        DB.Level topLevel = FindClosestLevel(currentTop, orderedLevels);

                        if (baseLevel == null || topLevel == null || baseLevel.Id.Equals(topLevel.Id))
                            continue;

                        // Create a column segment
                        CE.Column columnSegment = new CE.Column();

                        // Set location
                        columnSegment.StartPoint = new CG.Point2D(point.X * 12.0, point.Y * 12.0); // Convert to inches
                        columnSegment.EndPoint = columnSegment.StartPoint; // Same as start point for simple representation

                        // Set levels
                        if (levelIdMap.ContainsKey(baseLevel.Id) && levelIdMap.ContainsKey(topLevel.Id))
                        {
                            columnSegment.BaseLevelId = levelIdMap[baseLevel.Id];
                            columnSegment.TopLevelId = levelIdMap[topLevel.Id];
                        }
                        else
                        {
                            continue; // Skip if level mapping not found
                        }

                        // Set properties
                        columnSegment.FramePropertiesId = framePropertiesId;
                        columnSegment.Orientation = orientation;
                        columnSegment.IsLateral = isLateral;

                        columns.Add(columnSegment);
                        count++;

                        Debug.WriteLine($"Added column segment from {baseLevel.Name} to {topLevel.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting column: {ex.Message}");
                }
            }

            return count;
        }

        private List<double> FindIntersectingLevelElevations(double bottomElevation, double topElevation, List<DB.Level> levels)
        {
            List<double> intersections = new List<double>
    {
        bottomElevation // Always include bottom elevation
    };

            // Find all levels that intersect with the column
            foreach (var level in levels)
            {
                if (level.Elevation > bottomElevation && level.Elevation < topElevation)
                {
                    intersections.Add(level.Elevation);
                }
            }

            // Sort intersections by elevation
            intersections.Sort();
            return intersections;
        }

        private DB.Level FindClosestLevel(double elevation, List<DB.Level> levels)
        {
            if (levels == null || levels.Count == 0)
                return null;

            // Find the level with the closest elevation to the given value
            DB.Level closestLevel = levels[0];
            double minDiff = Math.Abs(levels[0].Elevation - elevation);

            foreach (var level in levels)
            {
                double diff = Math.Abs(level.Elevation - elevation);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestLevel = level;
                }
            }

            return closestLevel;
        }


        private List<DB.Level> GetOrderedLevels()
        {
            // Get all levels and order them by elevation
            DB.FilteredElementCollector levelCollector = new DB.FilteredElementCollector(_doc);
            return levelCollector.OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        private List<DB.Level> FindSpanningLevels(DB.Level baseLevel, DB.Level topLevel, List<DB.Level> orderedLevels)
        {
            // Find all levels between baseLevel and topLevel (inclusive)
            List<DB.Level> spanningLevels = new List<DB.Level>();
            bool inRange = false;

            foreach (var level in orderedLevels)
            {
                if (level.Id == baseLevel.Id)
                {
                    inRange = true;
                    spanningLevels.Add(level);
                }
                else if (level.Id == topLevel.Id)
                {
                    spanningLevels.Add(level);
                    break;
                }
                else if (inRange)
                {
                    spanningLevels.Add(level);
                }
            }

            return spanningLevels;
        }

        private double GetColumnBottomElevation(DB.FamilyInstance column)
        {
            // Get base level
            DB.Parameter baseLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
            if (baseLevelParam == null)
                return 0.0;

            DB.ElementId baseLevelId = baseLevelParam.AsElementId();
            if (baseLevelId == DB.ElementId.InvalidElementId)
                return 0.0;

            DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
            if (baseLevel == null)
                return 0.0;

            // Get base offset
            double baseOffset = 0.0;
            DB.Parameter baseOffsetParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
            if (baseOffsetParam != null && baseOffsetParam.HasValue)
            {
                baseOffset = baseOffsetParam.AsDouble();
            }

            // Calculate actual bottom elevation
            return baseLevel.Elevation + baseOffset;
        }

        private double GetColumnTopElevation(DB.FamilyInstance column)
        {
            // Get top level
            DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
            if (topLevelParam == null)
                return 0.0;

            DB.ElementId topLevelId = topLevelParam.AsElementId();
            if (topLevelId == DB.ElementId.InvalidElementId)
                return 0.0;

            DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;
            if (topLevel == null)
                return 0.0;

            // Get top offset
            double topOffset = 0.0;
            DB.Parameter topOffsetParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
            if (topOffsetParam != null && topOffsetParam.HasValue)
            {
                topOffset = topOffsetParam.AsDouble();
            }

            // Calculate actual top elevation
            return topLevel.Elevation + topOffset;
        }

        private CE.Column CreateColumnObject(DB.FamilyInstance revitColumn, DB.XYZ point, string framePropertiesId,
            Dictionary<DB.ElementId, string> levelIdMap, DB.Level baseLevel, DB.Level topLevel)
        {
            // Create a column object for the specified levels
            CE.Column column = new CE.Column();

            // Set column location
            column.StartPoint = new CG.Point2D(point.X * 12.0, point.Y * 12.0); // Convert to inches
            column.EndPoint = column.StartPoint; // Same as start point for simple representation

            // Set base and top levels using mapping
            if (levelIdMap.ContainsKey(baseLevel.Id) && levelIdMap.ContainsKey(topLevel.Id))
            {
                column.BaseLevelId = levelIdMap[baseLevel.Id];
                column.TopLevelId = levelIdMap[topLevel.Id];
            }
            else
            {
                return null; // Skip if level mapping not found
            }

            // Set column properties
            column.FramePropertiesId = framePropertiesId;

            // Try to get orientation parameter
            try
            {
                double orientation = GetColumnOrientation(revitColumn);
                column.Orientation = orientation;
            }
            catch { }

            // Determine if column is part of lateral system
            column.IsLateral = IsColumnLateral(revitColumn);

            return column;
        }

        private bool IsColumnLateral(DB.FamilyInstance column)
        {
            // Try to determine if column is part of lateral system
            try
            {
                // Check if column is part of a braced bay or moment frame (approximation)
                // Get column bounding box
                DB.BoundingBoxXYZ bbox = column.get_BoundingBox(null);
                if (bbox != null)
                {
                    DB.XYZ center = (bbox.Min + bbox.Max) / 2.0;

                    // Find braces near this column
                    DB.FilteredElementCollector braceCollector = new DB.FilteredElementCollector(_doc);
                    IList<DB.FamilyInstance> braces = braceCollector.OfClass(typeof(DB.FamilyInstance))
                        .OfCategory(DB.BuiltInCategory.OST_StructuralFraming)
                        .Cast<DB.FamilyInstance>()
                        .Where(f => f.StructuralType == DB.Structure.StructuralType.Brace)
                        .ToList();

                    // Check if any brace is near this column
                    foreach (var brace in braces)
                    {
                        DB.BoundingBoxXYZ braceBbox = brace.get_BoundingBox(null);
                        if (braceBbox != null)
                        {
                            DB.XYZ braceCenter = (braceBbox.Min + braceBbox.Max) / 2.0;
                            double distance = braceCenter.DistanceTo(center);
                            if (distance < 10.0) // 10 feet threshold
                                return true;
                        }
                    }

                    // Find beams with moment connections near this column
                    DB.FilteredElementCollector beamCollector = new DB.FilteredElementCollector(_doc);
                    IList<DB.FamilyInstance> beams = beamCollector.OfClass(typeof(DB.FamilyInstance))
                        .OfCategory(DB.BuiltInCategory.OST_StructuralFraming)
                        .Cast<DB.FamilyInstance>()
                        .Where(f => f.StructuralType == DB.Structure.StructuralType.Beam)
                        .ToList();

                    // Check if any beam with moment connection is near this column
                    foreach (var beam in beams)
                    {
                        // Check if beam has moment connection
                        bool hasMomentConnection = false;

                        DB.Parameter startConnectionParam = beam.get_Parameter(DB.BuiltInParameter.STRUCT_CONNECTION_BEAM_START);
                        if (startConnectionParam != null &&
                            startConnectionParam.HasValue &&
                            startConnectionParam.AsValueString().Equals("Moment Connection", StringComparison.OrdinalIgnoreCase))
                        {
                            hasMomentConnection = true;
                        }

                        DB.Parameter endConnectionParam = beam.get_Parameter(DB.BuiltInParameter.STRUCT_CONNECTION_BEAM_END);
                        if (endConnectionParam != null &&
                            endConnectionParam.HasValue &&
                            endConnectionParam.AsValueString().Equals("Moment Connection", StringComparison.OrdinalIgnoreCase))
                        {
                            hasMomentConnection = true;
                        }

                        if (hasMomentConnection)
                        {
                            DB.BoundingBoxXYZ beamBbox = beam.get_BoundingBox(null);
                            if (beamBbox != null)
                            {
                                DB.XYZ beamCenter = (beamBbox.Min + beamBbox.Max) / 2.0;
                                double distance = beamCenter.DistanceTo(center);
                                if (distance < 10.0) // 10 feet threshold
                                    return true;
                            }
                        }
                    }
                }

                // Check family/type naming conventions for lateral indicators
                string familyName = column.Symbol.FamilyName.ToUpper();
                string typeName = column.Symbol.Name.ToUpper();

                return familyName.Contains("MOMENT") ||
                       familyName.Contains("LATERAL") ||
                       typeName.Contains("MOMENT") ||
                       typeName.Contains("LATERAL");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error determining if column is lateral: {ex.Message}");
                // Default to false if any error occurs
            }

            return false;
        }

        private double GetColumnOrientation(DB.FamilyInstance column)
        {
            // Get the hand orientation vector
            DB.XYZ handOrientation = column.HandOrientation;

            // We only care about the X and Y components
            double x = handOrientation.X;
            double y = handOrientation.Y;

            // If both components are near zero, return default orientation
            if (Math.Abs(x) < 1e-6 && Math.Abs(y) < 1e-6)
                return 0.0;

            // Calculate the angle from the positive Y-axis
            double angleRad = Math.Atan2(x, y);

            // Convert to degrees
            double angleDeg = angleRad * 180.0 / Math.PI;

            // Make positive (0-360 range)
            if (angleDeg < 0)
                angleDeg += 360.0;

            // Map to 0-179 range using modulo
            angleDeg = angleDeg % 180.0;

            return angleDeg;
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
                    Math.Abs(l.Elevation - (revitLevel.Elevation * 12.0)) < 0.1);

                if (modelLevel != null)
                {
                    levelMap[revitLevel.Id] = modelLevel.Id;
                }
            }

            return levelMap;
        }

        private Dictionary<DB.ElementId, string> CreateFramePropertiesMapping(BaseModel model)
        {
            Dictionary<DB.ElementId, string> propsMap = new Dictionary<DB.ElementId, string>();

            // Get all family symbols
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FamilySymbol> famSymbols = collector.OfClass(typeof(DB.FamilySymbol))
                .Cast<DB.FamilySymbol>()
                .ToList();

            // Map each family symbol to the corresponding frame property in the model
            foreach (var symbol in famSymbols)
            {
                var frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                    fp.Name == symbol.Name);

                if (frameProperty != null)
                {
                    propsMap[symbol.Id] = frameProperty.Id;
                }
            }

            return propsMap;
        }
    }
}
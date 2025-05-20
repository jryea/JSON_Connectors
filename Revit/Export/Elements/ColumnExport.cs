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

            // Get ordered list of levels for splitting columns
            List<DB.Level> orderedLevels = GetOrderedLevels();

            foreach (var revitColumn in revitColumns)
            {
                try
                {
                    // Get column location
                    DB.LocationPoint location = revitColumn.Location as DB.LocationPoint;
                    if (location == null)
                        continue;

                    DB.XYZ point = location.Point;

                    // Get base and top levels
                    DB.Parameter baseLevelParam = revitColumn.get_Parameter(DB.BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                    DB.Parameter topLevelParam = revitColumn.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);

                    if (baseLevelParam == null || topLevelParam == null)
                        continue;

                    DB.ElementId baseLevelId = baseLevelParam.AsElementId();
                    DB.ElementId topLevelId = topLevelParam.AsElementId();

                    if (baseLevelId == DB.ElementId.InvalidElementId || topLevelId == DB.ElementId.InvalidElementId)
                        continue;

                    DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                    DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;

                    if (baseLevel == null || topLevel == null)
                        continue;

                    // Get column type
                    DB.ElementId typeId = revitColumn.GetTypeId();
                    string framePropertiesId = null;
                    if (framePropertiesMap.ContainsKey(typeId))
                        framePropertiesId = framePropertiesMap[typeId];

                    // Find all levels that this column spans
                    List<DB.Level> spanningLevels = FindSpanningLevels(baseLevel, topLevel, orderedLevels);

                    if (spanningLevels.Count <= 1)
                    {
                        // If column doesn't span multiple levels, export as a single column
                        CE.Column column = CreateColumnObject(revitColumn, point, framePropertiesId, levelIdMap, baseLevel, topLevel);
                        if (column != null)
                        {
                            columns.Add(column);
                            count++;
                        }
                    }
                    else
                    {
                        // Split column into segments for each level it spans
                        for (int i = 0; i < spanningLevels.Count - 1; i++)
                        {
                            DB.Level currentBaseLevel = spanningLevels[i];
                            DB.Level currentTopLevel = spanningLevels[i + 1];

                            CE.Column columnSegment = CreateColumnObject(revitColumn, point, framePropertiesId, levelIdMap,
                                currentBaseLevel, currentTopLevel);

                            if (columnSegment != null)
                            {
                                columns.Add(columnSegment);
                                count++;
                                Debug.WriteLine($"Added column segment from {currentBaseLevel.Name} to {currentTopLevel.Name}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting column: {ex.Message}");
                }
            }

            return count;
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
                DB.Parameter orientParam = revitColumn.get_Parameter(DB.BuiltInParameter.COLUMN_ORIENTATION_PARAM);
                if (orientParam != null && orientParam.HasValue)
                    column.Orientation = orientParam.AsDouble();
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
                // Look for lateral parameter
                DB.Parameter lateralParam = column.LookupParameter("Lateral");
                if (lateralParam != null && lateralParam.HasValue)
                {
                    if (lateralParam.StorageType == DB.StorageType.Integer)
                        return lateralParam.AsInteger() != 0;
                    else if (lateralParam.StorageType == DB.StorageType.String)
                        return lateralParam.AsString().ToUpper() == "YES" || lateralParam.AsString() == "1";
                }

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
                        .Where(f => {
                            DB.Structure.StructuralType structuralType = f.StructuralType;
                            return structuralType == DB.Structure.StructuralType.Brace;
                        })
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
                }
            }
            catch
            {
                // Default to false if any error occurs
            }

            return false;
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
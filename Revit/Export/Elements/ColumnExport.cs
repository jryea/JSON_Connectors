using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;
using Core.Models.Properties;
using Revit.Utilities;
using Core.Models;

namespace Revit.Export.Elements
{
    // Exports column elements from JSON into Revit
    public class ColumnExport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FamilySymbol> _columnTypes;

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

            // Get all levels sorted by elevation for easier lookup
            List<DB.Level> revitLevels = new DB.FilteredElementCollector(_doc)
                .OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .OrderBy(l => l.ProjectElevation)
                .ToList();

            // Create mappings
            Dictionary<DB.ElementId, string> levelIdMap = CreateLevelMapping(model);
            Dictionary<DB.ElementId, string> framePropertiesMap = CreateFramePropertiesMapping(model);

            foreach (var revitColumn in revitColumns)
            {
                try
                {
                    // Get column location
                    DB.LocationPoint location = revitColumn.Location as DB.LocationPoint;
                    if (location == null)
                        continue;

                    DB.XYZ point = location.Point;

                    // Create column object
                    CE.Column column = new CE.Column();

                    // Get base and top level IDs 
                    DB.ElementId baseLevelId = revitColumn.get_Parameter(DB.BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId();
                    DB.ElementId topLevelId = revitColumn.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId();

                    // Get base and top levels
                    DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                    DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;

                    if (baseLevel == null || topLevel == null)
                        continue;

                    // Get offset parameters
                    double baseOffset = 0;
                    double topOffset = 0;

                    // Get base level offset
                    DB.Parameter baseOffsetParam = revitColumn.get_Parameter(DB.BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                    if (baseOffsetParam != null && baseOffsetParam.HasValue)
                    {
                        baseOffset = baseOffsetParam.AsDouble();
                    }

                    // Get top level offset
                    DB.Parameter topOffsetParam = revitColumn.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                    if (topOffsetParam != null && topOffsetParam.HasValue)
                    {
                        topOffset = topOffsetParam.AsDouble();
                    }

                    // Calculate actual elevations with offsets
                    double actualBaseElevation = baseLevel.ProjectElevation + baseOffset;
                    double actualTopElevation = topLevel.ProjectElevation + topOffset;

                    Debug.WriteLine($"Column at ({point.X}, {point.Y}): Base elevation = {baseLevel.ProjectElevation}, offset = {baseOffset}, actual = {actualBaseElevation}");
                    Debug.WriteLine($"Column at ({point.X}, {point.Y}): Top elevation = {topLevel.ProjectElevation}, offset = {topOffset}, actual = {actualTopElevation}");

                    // Find the best matching levels by actual elevation
                    DB.Level matchedBaseLevel = FindClosestLevelByElevation(revitLevels, actualBaseElevation);
                    DB.Level matchedTopLevel = FindClosestLevelByElevation(revitLevels, actualTopElevation);

                    // Map levels to model level IDs 
                    if (levelIdMap.ContainsKey(matchedBaseLevel.Id))
                        column.BaseLevelId = levelIdMap[matchedBaseLevel.Id];

                    if (levelIdMap.ContainsKey(matchedTopLevel.Id))
                        column.TopLevelId = levelIdMap[matchedTopLevel.Id];
                  
                    // Set column type
                    DB.ElementId typeId = revitColumn.GetTypeId();
                    if (framePropertiesMap.ContainsKey(typeId))
                        column.FramePropertiesId = framePropertiesMap[typeId];

                    // Set column location
                    column.StartPoint = new CG.Point2D(point.X * 12.0, point.Y * 12.0); // Convert to inches
                    column.EndPoint = column.StartPoint; // Same as start point for simple representation

                    // Set column properties for lateral
                    column.IsLateral = false;

                    columns.Add(column);
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting column: {ex.Message}");
                    // Skip this column and continue with the next one
                }
            }

            return count;
        }

        private DB.Level FindClosestLevelByElevation(List<DB.Level> levels, double targetElevation)
        {
            if (levels == null || levels.Count == 0)
                return null;

            DB.Level closestLevel = null;
            double closestDistance = double.MaxValue;

            foreach (var level in levels)
            {
                double distance = Math.Abs(level.ProjectElevation - targetElevation);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestLevel = level;
                }
            }

            return closestLevel;
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
                    Math.Abs(l.Elevation - (revitLevel.ProjectElevation * 12.0)) < 0.1);

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
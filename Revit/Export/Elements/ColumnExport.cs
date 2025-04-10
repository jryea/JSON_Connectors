using System;
using System.Collections.Generic;
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

                    // Set base and top levels
                    DB.ElementId baseLevelId = revitColumn.get_Parameter(DB.BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId();
                    DB.ElementId topLevelId = revitColumn.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId();

                    if (levelIdMap.ContainsKey(baseLevelId))
                        column.BaseLevelId = levelIdMap[baseLevelId];

                    if (levelIdMap.ContainsKey(topLevelId))
                        column.TopLevelId = levelIdMap[topLevelId];
                    else
                        column.TopLevelId = column.BaseLevelId; // Default to base level if top level not found

                    // Set column type
                    DB.ElementId typeId = revitColumn.GetTypeId();
                    if (framePropertiesMap.ContainsKey(typeId))
                        column.FramePropertiesId = framePropertiesMap[typeId];

                    // Set column location
                    column.StartPoint = new CG.Point2D(point.X * 12.0, point.Y * 12.0); // Convert to inches
                    column.EndPoint = column.StartPoint; // Same as start point for simple representation

                    // Determine if column is part of lateral system (approximation)
                    column.IsLateral = IsColumnLateral(revitColumn);

                    columns.Add(column);
                    count++;
                }
                catch (Exception)
                {
                    // Skip this column and continue with the next one
                }
            }

            return count;
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
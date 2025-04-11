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

            // Group the Revit columns by their XY location
            var columnGroups = GroupColumnsByLocation(revitColumns);

            // Process each group of stacked columns
            foreach (var columnGroup in columnGroups)
            {
                var location = columnGroup.Key;
                var columnInstances = columnGroup.Value;

                // Get column properties to use for all instances
                DB.FamilyInstance sampleColumn = columnInstances.First();
                DB.LocationPoint locationPoint = sampleColumn.Location as DB.LocationPoint;
                if (locationPoint == null) continue;

                DB.XYZ point = locationPoint.Point;
                CG.Point2D columnPoint = new CG.Point2D(point.X * 12.0, point.Y * 12.0); // Convert to inches

                // Get the column type
                DB.ElementId typeId = sampleColumn.GetTypeId();
                string framePropertiesId = null;
                if (framePropertiesMap.ContainsKey(typeId))
                    framePropertiesId = framePropertiesMap[typeId];

                // Find all levels in the model
                var orderedLevels = model.ModelLayout.Levels
                    .OrderBy(l => l.Elevation)
                    .ToList();

                // Create columns spanning between consecutive levels
                // This mimics the stacked column pattern in the imported JSON
                for (int i = 0; i < orderedLevels.Count - 1; i++)
                {
                    string baseLevelId = orderedLevels[i].Id;
                    string topLevelId = orderedLevels[i + 1].Id;

                    // Create column object
                    CE.Column column = new CE.Column
                    {
                        StartPoint = columnPoint,
                        EndPoint = columnPoint, // Same as start point for simple representation
                        BaseLevelId = baseLevelId,
                        TopLevelId = topLevelId,
                        FramePropertiesId = framePropertiesId,
                        IsLateral = IsColumnLateral(sampleColumn)
                    };

                    columns.Add(column);
                    count++;
                }
            }

            return count;
        }

        // Group columns by their XY location
        private Dictionary<Tuple<double, double>, List<DB.FamilyInstance>> GroupColumnsByLocation(IList<DB.FamilyInstance> revitColumns)
        {
            var result = new Dictionary<Tuple<double, double>, List<DB.FamilyInstance>>();

            // Round to 3 decimal places for grouping by location
            const double tolerance = 0.001;

            foreach (var column in revitColumns)
            {
                var locationPoint = column.Location as DB.LocationPoint;
                if (locationPoint == null) continue;

                // Round the coordinates for reliable grouping
                double x = Math.Round(locationPoint.Point.X / tolerance) * tolerance;
                double y = Math.Round(locationPoint.Point.Y / tolerance) * tolerance;

                var key = Tuple.Create(x, y);

                if (!result.ContainsKey(key))
                {
                    result[key] = new List<DB.FamilyInstance>();
                }

                result[key].Add(column);
            }

            return result;
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
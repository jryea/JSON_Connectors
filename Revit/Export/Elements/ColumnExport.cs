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

            // Get all levels sorted by elevation for easier lookup
            List<DB.Level> revitLevels = new DB.FilteredElementCollector(_doc)
                .OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .OrderBy(l => l.ProjectElevation)
                .ToList();

            // Create mappings
            Dictionary<DB.ElementId, string> levelIdMap = CreateLevelMapping(model);
            Dictionary<DB.ElementId, string> framePropertiesMap = CreateFramePropertiesMapping(model);

            Debug.WriteLine($"Processing {revitColumns.Count} columns...");
            Debug.WriteLine($"Found {framePropertiesMap.Count} frame property mappings for columns");

            foreach (var revitColumn in revitColumns)
            {
                try
                {
                    // Get column location
                    DB.LocationPoint location = revitColumn.Location as DB.LocationPoint;
                    if (location == null)
                    {
                        Debug.WriteLine("Skipping column with no location point");
                        continue;
                    }

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
                    {
                        Debug.WriteLine($"Skipping column at ({point.X}, {point.Y}) due to missing base or top level");
                        continue;
                    }

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

                    Debug.WriteLine($"Column at ({point.X:F2}, {point.Y:F2}): Base level = {baseLevel.Name} ({baseLevel.ProjectElevation:F2} ft), offset = {baseOffset:F2}, actual = {actualBaseElevation:F2}");
                    Debug.WriteLine($"Column at ({point.X:F2}, {point.Y:F2}): Top level = {topLevel.Name} ({topLevel.ProjectElevation:F2} ft), offset = {topOffset:F2}, actual = {actualTopElevation:F2}");

                    // Find the best matching levels by *actual* elevation
                    DB.Level matchedBaseLevel = FindClosestLevelByElevation(revitLevels, actualBaseElevation);
                    DB.Level matchedTopLevel = FindClosestLevelByElevation(revitLevels, actualTopElevation);

                    Debug.WriteLine($"Column at ({point.X:F2}, {point.Y:F2}): Matched base level = {matchedBaseLevel?.Name ?? "None"}, Matched top level = {matchedTopLevel?.Name ?? "None"}");

                    // Map levels to model level IDs 
                    if (matchedBaseLevel != null && levelIdMap.ContainsKey(matchedBaseLevel.Id))
                    {
                        column.BaseLevelId = levelIdMap[matchedBaseLevel.Id];
                    }
                    else
                    {
                        Debug.WriteLine($"Could not find matching base level for column at ({point.X:F2}, {point.Y:F2})");
                        continue; // Skip column if we can't find a valid base level
                    }

                    if (matchedTopLevel != null && levelIdMap.ContainsKey(matchedTopLevel.Id))
                    {
                        column.TopLevelId = levelIdMap[matchedTopLevel.Id];
                    }
                    else
                    {
                        Debug.WriteLine($"Could not find matching top level for column at ({point.X:F2}, {point.Y:F2})");
                        continue; // Skip column if we can't find a valid top level
                    }

                    // Set column type - get the type ID and look it up in the mapping
                    DB.ElementId typeId = revitColumn.GetTypeId();

                    // Debug the type
                    DB.FamilySymbol symbol = _doc.GetElement(typeId) as DB.FamilySymbol;
                    Debug.WriteLine($"Column type: ID {typeId}, Name: {symbol?.Name}");

                    if (framePropertiesMap.ContainsKey(typeId))
                    {
                        column.FramePropertiesId = framePropertiesMap[typeId];
                        Debug.WriteLine($"Found frame properties ID in mapping: {column.FramePropertiesId}");
                    }
                    else
                    {
                        // Try to find a matching frame property by name
                        if (symbol != null)
                        {
                            // Capitalize any 'x' between numbers in the name (for consistent matching)
                            string capitalizedName = CapitalizeXBetweenNumbers(symbol.Name);

                            var frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                                string.Equals(fp.Name, capitalizedName, StringComparison.OrdinalIgnoreCase));

                            if (frameProperty != null)
                            {
                                column.FramePropertiesId = frameProperty.Id;
                                Debug.WriteLine($"Found frame properties by name match: {column.FramePropertiesId}");
                            }
                            else
                            {
                                Debug.WriteLine($"Could not find frame properties for column type: {capitalizedName}");

                                // Try partial name matching
                                frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                                    fp.Name.IndexOf(capitalizedName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    capitalizedName.IndexOf(fp.Name, StringComparison.OrdinalIgnoreCase) >= 0);

                                if (frameProperty != null)
                                {
                                    column.FramePropertiesId = frameProperty.Id;
                                    Debug.WriteLine($"Found frame properties by partial name match: {column.FramePropertiesId}");
                                }
                                else
                                {
                                    // Last resort: try to match by shape type
                                    string shape = DetermineShapeType(symbol);
                                    frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                                        fp.Shape == shape);

                                    if (frameProperty != null)
                                    {
                                        column.FramePropertiesId = frameProperty.Id;
                                        Debug.WriteLine($"Found frame properties by shape match: {column.FramePropertiesId}");
                                    }
                                }
                            }
                        }
                    }

                    // Set column location
                    column.StartPoint = new CG.Point2D(point.X * 12.0, point.Y * 12.0); // Convert to inches
                    column.EndPoint = column.StartPoint; // Same as start point for simple representation

                    // Determine if column is part of lateral system
                    column.IsLateral = IsColumnLateral(revitColumn);

                    // Log the column information
                    Debug.WriteLine($"Exporting column at ({point.X:F2}, {point.Y:F2}) with FramePropertiesId: {column.FramePropertiesId ?? "MISSING"}");

                    columns.Add(column);
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting column: {ex.Message}");
                    // Skip this column and continue with the next one
                }
            }

            Debug.WriteLine($"Exported {count} columns successfully");
            return count;
        }

        // Find the closest level to a target elevation
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

        // Helper method to capitalize any lowercase 'x' between numbers for consistent matching
        private string CapitalizeXBetweenNumbers(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string result = input;

            // Keep replacing until no more changes occur
            bool changed;
            do
            {
                string newResult = System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"(\d+)x(\d+)",
                    m => $"{m.Groups[1].Value}X{m.Groups[2].Value}"
                );

                changed = newResult != result;
                result = newResult;
            } while (changed);

            return result;
        }

        // Determine the shape type from a family symbol
        private string DetermineShapeType(DB.FamilySymbol famSymbol)
        {
            // Capitalize any lowercase 'x' between numbers in the family symbol name
            string typeName = CapitalizeXBetweenNumbers(famSymbol.Name).ToUpper();
            string famName = famSymbol.FamilyName.ToUpper();
            string combinedName = $"{famName} {typeName}";

            // For columns, check well-known steel shapes first
            if (typeName.StartsWith("W") && typeName.Contains("X"))
                return typeName; // Return full name for steel shapes
            if (combinedName.Contains("HSS") || combinedName.Contains("TUBE"))
                return typeName;
            if (typeName.StartsWith("HP") && typeName.Contains("X"))
                return typeName;

            // For rectangular/square columns that don't match standard steel shapes
            return "RECT";
        }

        // Determine if a column is part of the lateral system
        private bool IsColumnLateral(DB.FamilyInstance column)
        {
            try
            {
                // Try to get lateral parameter
                DB.Parameter lateralParam = column.LookupParameter("Lateral");
                if (lateralParam != null && lateralParam.HasValue)
                {
                    if (lateralParam.StorageType == DB.StorageType.Integer)
                        return lateralParam.AsInteger() != 0;
                    else if (lateralParam.StorageType == DB.StorageType.String)
                        return lateralParam.AsString().ToUpper() == "YES" || lateralParam.AsString() == "1";
                }

                // Check based on family and type name
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
                return false;
            }
        }

        // Create a mapping between Revit level IDs and model level IDs
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
                    Math.Abs(l.Elevation - (revitLevel.ProjectElevation * 12.0)) < 0.1); // Convert feet to inches with small tolerance

                if (modelLevel != null)
                {
                    levelMap[revitLevel.Id] = modelLevel.Id;
                    Debug.WriteLine($"Mapped Revit level '{revitLevel.Name}' ({revitLevel.ProjectElevation:F2} ft) to model level ID {modelLevel.Id}");
                }
                else
                {
                    Debug.WriteLine($"Could not find matching model level for Revit level '{revitLevel.Name}' ({revitLevel.ProjectElevation:F2} ft)");
                }
            }

            return levelMap;
        }

        // Create a mapping between Revit family symbol IDs and model frame property IDs
        private Dictionary<DB.ElementId, string> CreateFramePropertiesMapping(BaseModel model)
        {
            Dictionary<DB.ElementId, string> propsMap = new Dictionary<DB.ElementId, string>();

            // Get all family symbols with a focus on structural columns
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FamilySymbol> famSymbols = collector.OfClass(typeof(DB.FamilySymbol))
                .OfCategory(DB.BuiltInCategory.OST_StructuralColumns)
                .Cast<DB.FamilySymbol>()
                .ToList();

            // Debug available symbols
            Debug.WriteLine($"Found {famSymbols.Count} structural column family symbols");

            // Also get all frame properties for debugging
            Debug.WriteLine($"Model has {model.Properties.FrameProperties.Count} frame properties");
            foreach (var fp in model.Properties.FrameProperties)
            {
                Debug.WriteLine($"Frame property: {fp.Name}, ID: {fp.Id}, Shape: {fp.Shape}");
            }

            // Create a set to track processed types
            HashSet<string> processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Map each family symbol to the corresponding frame property in the model
            foreach (var symbol in famSymbols)
            {
                // Capitalize any 'x' between numbers in the name (for consistent matching)
                string capitalizedName = CapitalizeXBetweenNumbers(symbol.Name);

                // Skip if we've already processed this name
                if (processedNames.Contains(capitalizedName))
                {
                    Debug.WriteLine($"Skipping duplicate column type: {capitalizedName}");
                    continue;
                }

                processedNames.Add(capitalizedName);

                // Try direct name match first
                var frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                    string.Equals(fp.Name, capitalizedName, StringComparison.OrdinalIgnoreCase));

                if (frameProperty != null)
                {
                    propsMap[symbol.Id] = frameProperty.Id;
                    Debug.WriteLine($"Mapped column {symbol.Name} to {frameProperty.Name} ({frameProperty.Id})");
                }
                else
                {
                    // Try partial name match
                    frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                        fp.Name.IndexOf(capitalizedName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        capitalizedName.IndexOf(fp.Name, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (frameProperty != null)
                    {
                        propsMap[symbol.Id] = frameProperty.Id;
                        Debug.WriteLine($"Mapped column {symbol.Name} to {frameProperty.Name} ({frameProperty.Id}) by partial match");
                    }
                    else
                    {
                        Debug.WriteLine($"Could not find frame property for column type: {capitalizedName}");
                    }
                }
            }

            return propsMap;
        }
    }
}
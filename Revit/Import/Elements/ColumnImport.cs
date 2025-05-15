using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Revit.Utilities;

namespace Revit.Import.Elements
{
    public class ColumnImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FamilySymbol> _columnTypes;

        public ColumnImport(DB.Document doc)
        {
            _doc = doc;
            InitializeColumnTypes();
        }

        // Initialize dictionary of available column family types
        private void InitializeColumnTypes()
        {
            _columnTypes = new Dictionary<string, DB.FamilySymbol>();

            // Get all structural column family symbols
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.FamilySymbol));
            collector.OfCategory(DB.BuiltInCategory.OST_StructuralColumns);

            foreach (var symbol in collector)
            {
                DB.FamilySymbol familySymbol = symbol as DB.FamilySymbol;
                if (familySymbol == null)
                    continue;

                if (!familySymbol.IsActive)
                {
                    try { familySymbol.Activate(); }
                    catch { continue; }
                }

                string key = familySymbol.Name.ToUpper();
                if (!_columnTypes.ContainsKey(key))
                {
                    _columnTypes[key] = familySymbol;
                }

                // Also add by family name + symbol name for more specific matching
                string combinedKey = $"{familySymbol.Family.Name}_{familySymbol.Name}".ToUpper();
                if (!_columnTypes.ContainsKey(combinedKey))
                {
                    _columnTypes[combinedKey] = familySymbol;
                }
            }

            Debug.WriteLine($"Loaded {_columnTypes.Count} column family types");
        }

        // Find appropriate column type based on frame properties
        private DB.FamilySymbol FindColumnType(CE.Column column, Core.Models.Properties.FrameProperties frameProps)
        {
            // Default to the first column type if we can't find a match
            DB.FamilySymbol defaultType = _columnTypes.Values.FirstOrDefault();

            if (frameProps == null)
            {
                return defaultType;
            }

            // Try to match by name
            if (!string.IsNullOrEmpty(frameProps.Name))
            {
                string typeName = frameProps.Name.ToUpper();
                if (_columnTypes.TryGetValue(typeName, out DB.FamilySymbol typeByName))
                {
                    return typeByName;
                }
            }

            // Try to match by section type - first check if we have steel or concrete properties
            string sectionName = null;

            if (frameProps.SteelProps != null && !string.IsNullOrEmpty(frameProps.SteelProps.SectionName))
            {
                sectionName = frameProps.SteelProps.SectionName.ToUpper();
            }
            else if (frameProps.ConcreteProps != null && !string.IsNullOrEmpty(frameProps.ConcreteProps.SectionName))
            {
                sectionName = frameProps.ConcreteProps.SectionName.ToUpper();
            }

            if (sectionName != null && _columnTypes.TryGetValue(sectionName, out DB.FamilySymbol typeBySection))
            {
                return typeBySection;
            }

            // If still no match, try to find a column by material type
            if (frameProps.Type == FrameMaterialType.Concrete)
            {
                foreach (var pair in _columnTypes)
                {
                    if (pair.Key.Contains("CONCRETE") || pair.Key.Contains("CONC"))
                    {
                        return pair.Value;
                    }
                }
            }
            else if (frameProps.Type == FrameMaterialType.Steel)
            {
                // Try to find a steel column
                foreach (var pair in _columnTypes)
                {
                    if (pair.Key.Contains("STEEL") || pair.Key.Contains("METAL") ||
                        pair.Key.Contains("W") || pair.Key.Contains("HSS"))
                    {
                        return pair.Value;
                    }
                }
            }

            return defaultType;
        }

        // Get frame properties for a column
        private Core.Models.Properties.FrameProperties GetFrameProperties(CE.Column column, BaseModel model)
        {
            if (string.IsNullOrEmpty(column.FramePropertiesId) || model?.Properties?.FrameProperties == null)
            {
                return null;
            }

            return model.Properties.FrameProperties.FirstOrDefault(fp =>
                fp.Id == column.FramePropertiesId);
        }

        // Find floor thickness at a specific level
        private double GetFloorThicknessAtLevel(string levelId, BaseModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(levelId) || model?.Elements?.Floors == null)
                    return 0;

                // Find floors at this level
                var floors = model.Elements.Floors.Where(f => f.LevelId == levelId).ToList();
                if (!floors.Any())
                    return 0;

                // Get the floor properties for the first floor at this level
                var floor = floors.First();
                if (string.IsNullOrEmpty(floor.FloorPropertiesId) || model.Properties?.FloorProperties == null)
                    return 0;

                var floorProps = model.Properties.FloorProperties
                    .FirstOrDefault(fp => fp.Id == floor.FloorPropertiesId);

                if (floorProps == null)
                    return 0;

                // Return thickness in feet (convert from model units which are usually inches)
                return floorProps.Thickness / 12.0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting floor thickness: {ex.Message}");
                return 0; // Return zero thickness on error
            }
        }

        // Imports columns from the JSON model into Revit
        public int Import(List<CE.Column> columns, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;

            // Add robust error handling
            try
            {
                // Create a utility class to group columns
                var columnManager = new ColumnImportManager(_doc, levelIdMap);

                // Process each column from the model
                foreach (var jsonColumn in columns)
                {
                    try
                    {
                        // Skip if any required data is missing
                        if (string.IsNullOrEmpty(jsonColumn.BaseLevelId) ||
                            string.IsNullOrEmpty(jsonColumn.TopLevelId) ||
                            jsonColumn.StartPoint == null)
                        {
                            Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing data");
                            continue;
                        }

                        // Get the base and top level ElementIds
                        if (!levelIdMap.TryGetValue(jsonColumn.BaseLevelId, out DB.ElementId baseLevelId) ||
                            !levelIdMap.TryGetValue(jsonColumn.TopLevelId, out DB.ElementId topLevelId))
                        {
                            Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing level mapping");
                            continue;
                        }

                        // Get the levels
                        DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                        DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;
                        if (baseLevel == null || topLevel == null)
                        {
                            Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing levels");
                            continue;
                        }

                        // Log level elevations for debugging
                        double baseElevation = baseLevel.ProjectElevation;
                        double topElevation = topLevel.ProjectElevation;
                        Debug.WriteLine($"Base Level: {baseLevel.Name}, Elevation: {baseElevation}");
                        Debug.WriteLine($"Top Level: {topLevel.Name}, Elevation: {topElevation}");

                        // Skip if top level isn't higher than base level
                        if (topElevation <= baseElevation)
                        {
                            Debug.WriteLine($"Skipping column {jsonColumn.Id} because top level elevation is not higher than base level elevation");
                            continue;
                        }

                        // Get frame properties and find appropriate column type
                        var frameProps = GetFrameProperties(jsonColumn, model);
                        DB.FamilySymbol familySymbol = FindColumnType(jsonColumn, frameProps);

                        if (familySymbol == null)
                        {
                            Debug.WriteLine($"Skipping column {jsonColumn.Id} because no suitable family symbol could be found");
                            continue;
                        }

                        // Get column insertion point
                        DB.XYZ columnPoint = Helpers.ConvertToRevitCoordinates(jsonColumn.StartPoint);

                        // Calculate top offset based on floor thickness at the top level
                        double floorThickness = GetFloorThicknessAtLevel(jsonColumn.TopLevelId, model);
                        double topOffset = -floorThickness; // Negative to position below the floor

                        // Add to column manager for stacking
                        columnManager.AddColumn(jsonColumn.Id, columnPoint, baseLevel, topLevel,
                                               familySymbol, jsonColumn.IsLateral, frameProps, topOffset);
                    }
                    catch (Exception columnEx)
                    {
                        Debug.WriteLine($"Error processing column {jsonColumn.Id}: {columnEx.Message}");
                    }
                }

                // Create the stacked columns
                count = columnManager.CreateColumns();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical error in column import: {ex.Message}");
            }

            return count;
        }

        // Utility class to manage column stacking
        private class ColumnImportManager
        {
            private readonly DB.Document _doc;
            private readonly Dictionary<string, DB.ElementId> _levelIdMap;
            private readonly List<ColumnData> _columns = new List<ColumnData>();

            public ColumnImportManager(DB.Document doc, Dictionary<string, DB.ElementId> levelIdMap)
            {
                _doc = doc;
                _levelIdMap = levelIdMap;
            }

            public void AddColumn(string id, DB.XYZ location, DB.Level baseLevel, DB.Level topLevel,
                                 DB.FamilySymbol familySymbol, bool isLateral,
                                 Core.Models.Properties.FrameProperties frameProps, double topOffset)
            {
                _columns.Add(new ColumnData
                {
                    Id = id,
                    Location = location,
                    BaseLevel = baseLevel,
                    TopLevel = topLevel,
                    BaseLevelId = baseLevel.Id,
                    TopLevelId = topLevel.Id,
                    FamilySymbol = familySymbol,
                    IsLateral = isLateral,
                    FrameProps = frameProps,
                    TopOffset = topOffset
                });
            }

            public int CreateColumns()
            {
                int count = 0;

                try
                {
                    // Group columns by location (X,Y coordinates)
                    var columnGroups = _columns
                        .GroupBy(c => new { X = Math.Round(c.X, 3), Y = Math.Round(c.Y, 3) })
                        .ToList();

                    Debug.WriteLine($"Found {columnGroups.Count} column groups after combining by location");

                    // Process each column group (columns at the same location)
                    foreach (var locationGroup in columnGroups)
                    {
                        try
                        {
                            // Group further by family symbol
                            var symbolGroups = locationGroup.GroupBy(c => c.FamilySymbol.Id).ToList();

                            foreach (var symbolGroup in symbolGroups)
                            {
                                // Sort by base level elevation
                                var sortedColumns = symbolGroup.OrderBy(c => c.BaseLevel.Elevation).ToList();

                                // Find stacked columns (one's top level = next one's base level)
                                List<List<ColumnData>> stackedGroups = new List<List<ColumnData>>();
                                List<ColumnData> currentStack = new List<ColumnData>();
                                currentStack.Add(sortedColumns[0]);

                                for (int i = 1; i < sortedColumns.Count; i++)
                                {
                                    var prevColumn = sortedColumns[i - 1];
                                    var currColumn = sortedColumns[i];

                                    // Check if current column's base level is the same as previous column's top level
                                    if (prevColumn.TopLevel.Id == currColumn.BaseLevel.Id)
                                    {
                                        // This is part of the stack
                                        currentStack.Add(currColumn);
                                    }
                                    else
                                    {
                                        // Start a new stack
                                        stackedGroups.Add(currentStack);
                                        currentStack = new List<ColumnData>();
                                        currentStack.Add(currColumn);
                                    }
                                }

                                // Add the last stack
                                if (currentStack.Count > 0)
                                {
                                    stackedGroups.Add(currentStack);
                                }

                                Debug.WriteLine($"Found {stackedGroups.Count} stacked column groups at location {locationGroup.Key.X}, {locationGroup.Key.Y}");

                                // Create each stacked column
                                foreach (var stack in stackedGroups)
                                {
                                    try
                                    {
                                        // Use the bottom column's base level and the top column's top level
                                        var bottomColumn = stack.First();
                                        var topColumn = stack.Last();

                                        // Check for existing columns at this point
                                        bool columnExists = false;
                                        var existingColumns = new DB.FilteredElementCollector(_doc)
                                            .OfClass(typeof(DB.FamilyInstance))
                                            .OfCategory(DB.BuiltInCategory.OST_StructuralColumns)
                                            .WhereElementIsNotElementType()
                                            .ToElements();

                                        foreach (var existingColumn in existingColumns)
                                        {
                                            var location = existingColumn.Location as DB.LocationPoint;
                                            if (location != null && location.Point.IsAlmostEqualTo(bottomColumn.Location))
                                            {
                                                Debug.WriteLine($"A column already exists at point {bottomColumn.Location}. Skipping stacked column.");
                                                columnExists = true;
                                                break;
                                            }
                                        }

                                        if (columnExists)
                                        {
                                            continue;
                                        }

                                        // Create the consolidated column
                                        DB.FamilyInstance column = _doc.Create.NewFamilyInstance(
                                            bottomColumn.Location,
                                            bottomColumn.FamilySymbol,
                                            bottomColumn.BaseLevel,
                                            DB.Structure.StructuralType.Column);

                                        if (column == null)
                                        {
                                            Debug.WriteLine($"Failed to create stacked column at {bottomColumn.Location}.");
                                            continue;
                                        }

                                        // Set top level to the top column's top level
                                        try
                                        {
                                            DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                                            if (topLevelParam != null && !topLevelParam.IsReadOnly)
                                            {
                                                topLevelParam.Set(topColumn.TopLevelId);
                                            }

                                            // Set top offset based on floor thickness
                                            DB.Parameter topOffsetParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                                            if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                                            {
                                                topOffsetParam.Set(topColumn.TopOffset);
                                                Debug.WriteLine($"Set top offset to {topColumn.TopOffset} feet for column at {bottomColumn.Location}");
                                            }
                                        }
                                        catch (Exception paramEx)
                                        {
                                            Debug.WriteLine($"Error setting column parameters: {paramEx.Message}");
                                        }

                                        // Log stacked column creation
                                        string columnIds = string.Join(", ", stack.Select(c => c.Id));
                                        Debug.WriteLine($"Created stacked column from {stack.Count} columns ({columnIds}) from level {bottomColumn.BaseLevel.Name} to {topColumn.TopLevel.Name}");
                                        count++;
                                    }
                                    catch (Exception stackEx)
                                    {
                                        Debug.WriteLine($"Error creating stacked column: {stackEx.Message}");
                                    }
                                }
                            }
                        }
                        catch (Exception groupEx)
                        {
                            Debug.WriteLine($"Error processing column group at {locationGroup.Key.X}, {locationGroup.Key.Y}: {groupEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating columns: {ex.Message}");
                }

                return count;
            }
        }

        private class ColumnData
        {
            public string Id { get; set; }
            public DB.XYZ Location { get; set; }
            public DB.Level BaseLevel { get; set; }
            public DB.Level TopLevel { get; set; }
            public DB.ElementId BaseLevelId { get; set; }
            public DB.ElementId TopLevelId { get; set; }
            public DB.FamilySymbol FamilySymbol { get; set; }
            public bool IsLateral { get; set; }
            public Core.Models.Properties.FrameProperties FrameProps { get; set; }
            public double TopOffset { get; set; } = 0;
            public double X => Location.X;
            public double Y => Location.Y;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Core.Models.Properties;
using Revit.Utilities;

namespace Revit.Import.Elements
{
    public class ColumnImport
    {
        private readonly DB.Document _doc;
        private readonly Dictionary<string, DB.FamilySymbol> _columnTypes;

        public ColumnImport(DB.Document doc)
        {
            _doc = doc;
            _columnTypes = LoadColumnTypes();
        }

        // Initialize dictionary of available column family types
        private Dictionary<string, DB.FamilySymbol> LoadColumnTypes()
        {
            var columnTypes = new Dictionary<string, DB.FamilySymbol>();

            try
            {
                // Get all structural column family symbols
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                collector.OfClass(typeof(DB.FamilySymbol));
                collector.OfCategory(DB.BuiltInCategory.OST_StructuralColumns);

                foreach (var symbol in collector)
                {
                    DB.FamilySymbol familySymbol = symbol as DB.FamilySymbol;
                    if (familySymbol == null)
                        continue;

                    // Activate the family symbol if it's not active
                    if (!familySymbol.IsActive)
                    {
                        try
                        {
                            familySymbol.Activate();
                            Debug.WriteLine($"Activated column family symbol: {familySymbol.Name}");
                        }
                        catch (Exception activateEx)
                        {
                            Debug.WriteLine($"Failed to activate column family symbol {familySymbol.Name}: {activateEx.Message}");
                            continue;
                        }
                    }

                    // Add by symbol name
                    string key = familySymbol.Name.ToUpper();
                    if (!columnTypes.ContainsKey(key))
                    {
                        columnTypes[key] = familySymbol;
                        Debug.WriteLine($"Loaded column type: {key}");
                    }

                    // Also add by family name + symbol name for more specific matching
                    string combinedKey = $"{familySymbol.Family.Name}_{familySymbol.Name}".ToUpper();
                    if (!columnTypes.ContainsKey(combinedKey))
                    {
                        columnTypes[combinedKey] = familySymbol;
                        Debug.WriteLine($"Loaded column type (combined): {combinedKey}");
                    }
                }

                Debug.WriteLine($"Total column types loaded: {columnTypes.Count}");

                if (columnTypes.Count == 0)
                {
                    Debug.WriteLine("WARNING: No column types found in the document!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading column types: {ex.Message}");
            }

            return columnTypes;
        }

        // Get floor thickness from BaseModel for a specific level (returns value in feet)
        private double GetFloorThicknessForLevel(string levelId, BaseModel model)
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

                // Convert thickness from inches (model units) to feet (Revit units)
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

            Debug.WriteLine($"ColumnImport.Import called with {columns?.Count ?? 0} columns");

            if (columns == null || columns.Count == 0)
            {
                Debug.WriteLine("No columns to import");
                return 0;
            }

            if (_columnTypes.Count == 0)
            {
                Debug.WriteLine("ERROR: No column types available in document!");
                return 0;
            }

            try
            {
                // Create a utility class to group columns
                var columnManager = new ColumnImportManager(_doc, levelIdMap);

                Debug.WriteLine("Processing columns...");

                // Process each column from the model
                foreach (var jsonColumn in columns)
                {
                    try
                    {
                        Debug.WriteLine($"Processing column {jsonColumn.Id}");

                        // Skip if any required data is missing
                        if (string.IsNullOrEmpty(jsonColumn.BaseLevelId) ||
                            string.IsNullOrEmpty(jsonColumn.TopLevelId) ||
                            jsonColumn.StartPoint == null)
                        {
                            Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing data");
                            Debug.WriteLine($"  BaseLevelId: {jsonColumn.BaseLevelId ?? "NULL"}");
                            Debug.WriteLine($"  TopLevelId: {jsonColumn.TopLevelId ?? "NULL"}");
                            Debug.WriteLine($"  StartPoint: {(jsonColumn.StartPoint == null ? "NULL" : "OK")}");
                            continue;
                        }

                        // Get the base and top level ElementIds
                        if (!levelIdMap.TryGetValue(jsonColumn.BaseLevelId, out DB.ElementId baseLevelId) ||
                            !levelIdMap.TryGetValue(jsonColumn.TopLevelId, out DB.ElementId topLevelId))
                        {
                            Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing level mapping");
                            Debug.WriteLine($"  BaseLevelId '{jsonColumn.BaseLevelId}' found: {levelIdMap.ContainsKey(jsonColumn.BaseLevelId)}");
                            Debug.WriteLine($"  TopLevelId '{jsonColumn.TopLevelId}' found: {levelIdMap.ContainsKey(jsonColumn.TopLevelId)}");
                            continue;
                        }

                        // Get the levels
                        DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                        DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;
                        if (baseLevel == null || topLevel == null)
                        {
                            Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing levels");
                            Debug.WriteLine($"  BaseLevel: {(baseLevel == null ? "NULL" : baseLevel.Name)}");
                            Debug.WriteLine($"  TopLevel: {(topLevel == null ? "NULL" : topLevel.Name)}");
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
                            Debug.WriteLine($"Available column types: {string.Join(", ", _columnTypes.Keys)}");
                            continue;
                        }

                        // Make sure the family symbol is active
                        if (!familySymbol.IsActive)
                        {
                            try
                            {
                                familySymbol.Activate();
                                Debug.WriteLine($"Activated family symbol for column {jsonColumn.Id}: {familySymbol.Name}");
                            }
                            catch (Exception activateEx)
                            {
                                Debug.WriteLine($"Error activating family symbol for column {jsonColumn.Id}: {activateEx.Message}");
                                continue;
                            }
                        }

                        // Log orientation information
                        if (jsonColumn.Orientation != 0)
                        {
                            Debug.WriteLine($"Column {jsonColumn.Id} has non-default orientation: {jsonColumn.Orientation} degrees");
                        }

                        // Get column insertion point
                        DB.XYZ columnPoint = Helpers.ConvertToRevitCoordinates(jsonColumn.StartPoint);

                        // Calculate top offset based on floor thickness at the top level
                        double floorThickness = GetFloorThicknessForLevel(jsonColumn.TopLevelId, model);
                        double topOffset = -floorThickness; // Negative to position below the floor

                        Debug.WriteLine($"Column {jsonColumn.Id}: Floor thickness = {floorThickness}, Top offset = {topOffset}");

                        // Add to column manager for creation
                        columnManager.AddColumn(jsonColumn.Id, columnPoint, baseLevel, topLevel,
                                              familySymbol, jsonColumn.IsLateral, frameProps,
                                              jsonColumn.Orientation, topOffset);

                        Debug.WriteLine($"Added column {jsonColumn.Id} to column manager for creation");
                    }
                    catch (Exception colEx)
                    {
                        Debug.WriteLine($"Error processing column {jsonColumn.Id}: {colEx.Message}");
                        Debug.WriteLine($"  Stack trace: {colEx.StackTrace}");
                    }
                }

                Debug.WriteLine("Finished processing individual columns, now creating them...");

                // Create all columns
                count = columnManager.CreateColumns();

                Debug.WriteLine($"Column creation completed. Created {count} columns total.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical error in column import: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return count;
        }

        // Find appropriate column type based on JSON data and frame properties
        private DB.FamilySymbol FindColumnType(CE.Column jsonColumn, FrameProperties frameProps)
        {
            try
            {
                DB.FamilySymbol bestMatch = null;

                // First, try to find a column by frame properties if available
                if (frameProps != null)
                {
                    bestMatch = FindColumnByFrameProperties(frameProps);
                    if (bestMatch != null)
                    {
                        Debug.WriteLine($"Found column type by frame properties: {bestMatch.Name}");
                        return bestMatch;
                    }
                }

                // Fallback to finding any concrete column
                bestMatch = FindFallbackConcreteColumn();
                if (bestMatch != null)
                {
                    Debug.WriteLine($"Using fallback concrete column: {bestMatch.Name}");
                }

                return bestMatch;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding column type: {ex.Message}");
                return null;
            }
        }

        // Find column type based on frame properties
        private DB.FamilySymbol FindColumnByFrameProperties(FrameProperties frameProps)
        {
            try
            {
                if (frameProps == null) return null;

                // Look for columns that match the material type
                bool isConcrete = frameProps.Type == FrameMaterialType.Concrete ||
                                frameProps.MaterialId?.ToUpper().Contains("CONCRETE") == true ||
                                frameProps.MaterialId?.ToUpper().Contains("CONC") == true;

                bool isSteel = frameProps.Type == FrameMaterialType.Steel ||
                             frameProps.MaterialId?.ToUpper().Contains("STEEL") == true ||
                             frameProps.MaterialId?.ToUpper().Contains("A992") == true;

                string materialFilter = isConcrete ? "CONCRETE" : isSteel ? "STEEL" : "";

                // Try to find exact match first
                if (!string.IsNullOrEmpty(materialFilter))
                {
                    var matchingColumns = _columnTypes.Where(kvp =>
                        kvp.Key.Contains(materialFilter)).ToList();

                    if (matchingColumns.Any())
                    {
                        var bestMatch = matchingColumns.First().Value;

                        // Try to set dimensions if it's a concrete column with dimensions
                        if (isConcrete && frameProps.ConcreteProps != null)
                        {
                            double width = frameProps.ConcreteProps.Width;
                            double depth = frameProps.ConcreteProps.Depth;

                            if (width > 0 && depth > 0)
                            {
                                SetColumnDimensions(bestMatch, width, depth);
                            }
                        }

                        return bestMatch;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindColumnByFrameProperties: {ex.Message}");
                return null;
            }
        }

        // Set column dimensions for parametric families
        private bool SetColumnDimensions(DB.FamilySymbol columnType, double widthInches, double depthInches)
        {
            bool success = false;

            try
            {
                // Convert from inches to feet
                double widthFeet = widthInches / 12.0;
                double depthFeet = depthInches / 12.0;

                // Common parameter names for column dimensions
                string[] widthParamNames = { "Width", "b", "Column_Width", "Dimension_Width" };
                string[] depthParamNames = { "Depth", "h", "Column_Depth" };

                Debug.WriteLine($"Setting column dimensions: Width={widthFeet:F3}', Depth={depthFeet:F3}'");

                // Try to set width parameter
                foreach (string paramName in widthParamNames)
                {
                    var widthParam = columnType.LookupParameter(paramName);
                    if (widthParam != null && !widthParam.IsReadOnly && widthParam.StorageType == DB.StorageType.Double)
                    {
                        widthParam.Set(widthFeet);
                        Debug.WriteLine($"  Set width parameter '{paramName}' = {widthFeet:F3}'");
                        success = true;
                        break;
                    }
                }

                // Try to set depth parameter
                foreach (string paramName in depthParamNames)
                {
                    var depthParam = columnType.LookupParameter(paramName);
                    if (depthParam != null && !depthParam.IsReadOnly && depthParam.StorageType == DB.StorageType.Double)
                    {
                        depthParam.Set(depthFeet);
                        Debug.WriteLine($"  Set depth parameter '{paramName}' = {depthFeet:F3}'");
                        success = true;
                        break;
                    }
                }

                if (!success)
                {
                    Debug.WriteLine("  Warning: Could not find suitable width/depth parameters");
                    LogAvailableParameters(columnType);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting column dimensions: {ex.Message}");
            }

            return success;
        }

        private void LogAvailableParameters(DB.FamilySymbol columnType)
        {
            try
            {
                Debug.WriteLine($"Available parameters for {columnType.Name}:");
                foreach (DB.Parameter param in columnType.Parameters)
                {
                    if (param.StorageType == DB.StorageType.Double)
                    {
                        string readOnlyStatus = param.IsReadOnly ? " (ReadOnly)" : "";
                        Debug.WriteLine($"  - {param.Definition.Name}: {param.StorageType}{readOnlyStatus}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error logging parameters: {ex.Message}");
            }
        }

        private DB.FamilySymbol FindFallbackConcreteColumn()
        {
            // Find any concrete column as fallback
            var concreteColumns = _columnTypes.Where(kvp =>
                kvp.Key.Contains("CONCRETE") ||
                kvp.Key.Contains("CONC"))
                .ToList();

            if (concreteColumns.Any())
                return concreteColumns.First().Value;

            // Ultimate fallback
            return _columnTypes.Values.FirstOrDefault();
        }

        // Get frame properties for a column
        private FrameProperties GetFrameProperties(CE.Column column, BaseModel model)
        {
            if (string.IsNullOrEmpty(column.FramePropertiesId) || model?.Properties?.FrameProperties == null)
            {
                return null;
            }

            return model.Properties.FrameProperties.FirstOrDefault(fp =>
                fp.Id == column.FramePropertiesId);
        }
    }

    // Column management helper class
    public class ColumnImportManager
    {
        private readonly DB.Document _doc;
        private readonly Dictionary<string, DB.ElementId> _levelIdMap;
        private readonly List<ColumnData> _columns;
        private readonly Dictionary<string, DB.FamilyInstance> _createdColumns;

        public ColumnImportManager(DB.Document doc, Dictionary<string, DB.ElementId> levelIdMap)
        {
            _doc = doc;
            _levelIdMap = levelIdMap;
            _columns = new List<ColumnData>();
            _createdColumns = new Dictionary<string, DB.FamilyInstance>();
        }

        public void AddColumn(string id, DB.XYZ location, DB.Level baseLevel, DB.Level topLevel,
                             DB.FamilySymbol familySymbol, bool isLateral,
                             FrameProperties frameProps, double orientation = 0, double topOffset = 0)
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
                Orientation = orientation,
                TopOffset = topOffset
            });
        }

        public int CreateColumns()
        {
            int count = 0;

            Debug.WriteLine($"CreateColumns called with {_columns.Count} columns to process");

            if (_columns.Count == 0)
            {
                Debug.WriteLine("No columns to create - returning 0");
                return 0;
            }

            // Group columns by location to determine stacking strategy
            var columnGroups = _columns.GroupBy(c => new {
                X = Math.Round(c.Location.X, 3),
                Y = Math.Round(c.Location.Y, 3)
            }).ToList();

            Debug.WriteLine($"Creating columns in {columnGroups.Count} location groups");

            foreach (var group in columnGroups)
            {
                var columnsAtLocation = group.OrderBy(c => c.BaseLevel.Elevation).ToList();

                Debug.WriteLine($"Processing location group with {columnsAtLocation.Count} columns");

                if (ShouldStackColumns(columnsAtLocation))
                {
                    Debug.WriteLine("Creating stacked columns");
                    count += CreateStackedColumns(columnsAtLocation);
                }
                else
                {
                    Debug.WriteLine("Creating individual columns");
                    count += CreateIndividualColumns(columnsAtLocation);
                }
            }

            Debug.WriteLine($"CreateColumns finished - created {count} columns total");
            return count;
        }

        // Determine if columns should be stacked
        private bool ShouldStackColumns(List<ColumnData> columns)
        {
            if (columns.Count <= 1) return false;

            // Check if columns form a continuous vertical stack
            for (int i = 0; i < columns.Count - 1; i++)
            {
                var current = columns[i];
                var next = columns[i + 1];

                // If top of current != base of next, can't stack
                if (current.TopLevelId.IntegerValue != next.BaseLevelId.IntegerValue)
                {
                    return false;
                }
            }

            // If all columns use the same family symbol and have similar properties, stack them
            var firstColumn = columns.First();
            return columns.All(c => c.FamilySymbol.Id == firstColumn.FamilySymbol.Id);
        }

        // Create individual columns when stacking isn't appropriate
        private int CreateIndividualColumns(List<ColumnData> columns)
        {
            int count = 0;

            Debug.WriteLine($"CreateIndividualColumns called with {columns.Count} columns");

            foreach (var columnData in columns)
            {
                try
                {
                    Debug.WriteLine($"Creating individual column {columnData.Id} at location {columnData.Location}");
                    Debug.WriteLine($"  Base Level: {columnData.BaseLevel.Name} (ID: {columnData.BaseLevelId})");
                    Debug.WriteLine($"  Top Level: {columnData.TopLevel.Name} (ID: {columnData.TopLevelId})");
                    Debug.WriteLine($"  Family Symbol: {columnData.FamilySymbol.Name}");
                    Debug.WriteLine($"  Top Offset: {columnData.TopOffset}");

                    // Create column from base to top level
                    DB.FamilyInstance column = _doc.Create.NewFamilyInstance(
                        columnData.Location,
                        columnData.FamilySymbol,
                        columnData.BaseLevel,
                        DB.Structure.StructuralType.Column);

                    if (column == null)
                    {
                        Debug.WriteLine($"ERROR: Failed to create column {columnData.Id} - NewFamilyInstance returned null");
                        continue;
                    }

                    Debug.WriteLine($"Successfully created column instance {column.Id} for {columnData.Id}");

                    // Set top level and offset
                    try
                    {
                        DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                        if (topLevelParam != null && !topLevelParam.IsReadOnly)
                        {
                            topLevelParam.Set(columnData.TopLevelId);
                            Debug.WriteLine($"  Set top level parameter successfully");
                        }
                        else
                        {
                            Debug.WriteLine($"  WARNING: Top level parameter is null or read-only");
                        }

                        // Apply top offset based on floor thickness
                        DB.Parameter topOffsetParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                        if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                        {
                            topOffsetParam.Set(columnData.TopOffset);
                            Debug.WriteLine($"  Applied top offset of {columnData.TopOffset} feet");
                        }
                        else
                        {
                            Debug.WriteLine($"  WARNING: Top offset parameter is null or read-only");
                        }

                        // Apply rotation if needed
                        ApplyColumnRotation(column, columnData);

                        Debug.WriteLine($"Created individual column {columnData.Id} from {columnData.BaseLevel.Name} to {columnData.TopLevel.Name}");
                        _createdColumns[columnData.Id] = column;
                        count++;
                    }
                    catch (Exception paramEx)
                    {
                        Debug.WriteLine($"Error setting parameters for column {columnData.Id}: {paramEx.Message}");
                        Debug.WriteLine($"  Stack trace: {paramEx.StackTrace}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating individual column {columnData.Id}: {ex.Message}");
                    Debug.WriteLine($"  Stack trace: {ex.StackTrace}");
                }
            }

            Debug.WriteLine($"CreateIndividualColumns finished - created {count} out of {columns.Count} columns");
            return count;
        }

        // Create optimally stacked columns
        private int CreateStackedColumns(List<ColumnData> columns)
        {
            int count = 0;

            try
            {
                // Sort columns by base level elevation
                var sortedColumns = columns.OrderBy(c => c.BaseLevel.Elevation).ToList();

                // Identify continuous column stacks
                List<List<ColumnData>> stacks = FindContinuousStacks(sortedColumns);

                // Create each stack
                foreach (var stack in stacks)
                {
                    try
                    {
                        if (stack.Count == 0) continue;

                        var bottomColumn = stack.First();
                        var topColumn = stack.Last();

                        // Create the column
                        DB.FamilyInstance column = _doc.Create.NewFamilyInstance(
                            bottomColumn.Location,
                            bottomColumn.FamilySymbol,
                            bottomColumn.BaseLevel,
                            DB.Structure.StructuralType.Column);

                        if (column == null)
                        {
                            Debug.WriteLine($"Failed to create stacked column at {bottomColumn.Location}");
                            continue;
                        }

                        // Set top level and offset
                        try
                        {
                            DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (topLevelParam != null && !topLevelParam.IsReadOnly)
                            {
                                topLevelParam.Set(topColumn.TopLevelId);
                            }

                            // Apply top offset from the topmost column
                            DB.Parameter topOffsetParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                            {
                                topOffsetParam.Set(topColumn.TopOffset);
                                Debug.WriteLine($"Stacked column: Applied top offset of {topColumn.TopOffset} feet");
                            }

                            // Apply rotation from bottom column
                            ApplyColumnRotation(column, bottomColumn);

                            Debug.WriteLine($"Created stacked column from {bottomColumn.BaseLevel.Name} to {topColumn.TopLevel.Name}");

                            // Store reference for all columns in the stack
                            foreach (var stackColumn in stack)
                            {
                                _createdColumns[stackColumn.Id] = column;
                            }

                            count++;
                        }
                        catch (Exception paramEx)
                        {
                            Debug.WriteLine($"Error setting parameters for stacked column: {paramEx.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error creating stacked column: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CreateStackedColumns: {ex.Message}");
            }

            return count;
        }

        // Apply rotation to column if needed
        private void ApplyColumnRotation(DB.FamilyInstance column, ColumnData columnData)
        {
            try
            {
                if (Math.Abs(columnData.Orientation) > 0.001) // Only rotate if orientation is not zero
                {
                    double rotationRadians = columnData.Orientation * Math.PI / 180.0; // Convert to radians
                    DB.LocationPoint location = column.Location as DB.LocationPoint;
                    if (location != null)
                    {
                        DB.Line rotationAxis = DB.Line.CreateBound(
                            location.Point,
                            location.Point + DB.XYZ.BasisZ);

                        location.Rotate(rotationAxis, rotationRadians);
                        Debug.WriteLine($"Applied rotation of {columnData.Orientation} degrees to column");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying column rotation: {ex.Message}");
            }
        }

        // Find continuous stacks of columns
        private List<List<ColumnData>> FindContinuousStacks(List<ColumnData> sortedColumns)
        {
            var stacks = new List<List<ColumnData>>();
            if (sortedColumns.Count == 0) return stacks;

            var currentStack = new List<ColumnData> { sortedColumns[0] };

            for (int i = 1; i < sortedColumns.Count; i++)
            {
                var prevColumn = sortedColumns[i - 1];
                var currColumn = sortedColumns[i];

                // Check if current column continues the stack (top of previous = base of current)
                if (prevColumn.TopLevelId.IntegerValue == currColumn.BaseLevelId.IntegerValue)
                {
                    // Continue the stack
                    currentStack.Add(currColumn);
                }
                else
                {
                    // End current stack and start a new one
                    stacks.Add(new List<ColumnData>(currentStack));
                    currentStack.Clear();
                    currentStack.Add(currColumn);
                }
            }

            // Add the final stack if it's not empty
            if (currentStack.Count > 0)
            {
                stacks.Add(currentStack);
            }

            return stacks;
        }
    }

    // Data structure to hold column information
    public class ColumnData
    {
        public string Id { get; set; }
        public DB.XYZ Location { get; set; }
        public DB.Level BaseLevel { get; set; }
        public DB.Level TopLevel { get; set; }
        public DB.ElementId BaseLevelId { get; set; }
        public DB.ElementId TopLevelId { get; set; }
        public DB.FamilySymbol FamilySymbol { get; set; }
        public bool IsLateral { get; set; }
        public FrameProperties FrameProps { get; set; }
        public double Orientation { get; set; }
        public double TopOffset { get; set; }
    }
}
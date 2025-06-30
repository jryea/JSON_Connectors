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
                        // Use negative value so column stops at bottom of floor
                        double topOffset = -GetFloorThicknessForLevel(jsonColumn.TopLevelId, model);

                        // Add column to manager
                        columnManager.AddColumn(
                            jsonColumn.Id,
                            columnPoint,
                            baseLevel,
                            topLevel,
                            familySymbol,
                            jsonColumn.IsLateral,
                            frameProps,
                            jsonColumn.Orientation,
                            topOffset);

                        Debug.WriteLine($"Added column {jsonColumn.Id} to manager for processing");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing column {jsonColumn.Id}: {ex.Message}");
                        Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }

                // Use the manager to create columns optimally
                count = columnManager.CreateColumns();

                Debug.WriteLine($"ColumnImport completed: {count} columns created out of {columns.Count} attempted");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ColumnImport.Import: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }

            return count;
        }

        // Find appropriate column type for the given column and properties
        private DB.FamilySymbol FindColumnType(CE.Column column, FrameProperties frameProps)
        {
            try
            {
                string sectionName = null;

                // Try to get section name from frame properties
                if (frameProps != null && !string.IsNullOrEmpty(frameProps.Name))
                {
                    sectionName = frameProps.Name.ToUpper();
                    Debug.WriteLine($"Column {column.Id}: Using frame properties section '{sectionName}'");
                }

                // Search for matching column type
                if (!string.IsNullOrEmpty(sectionName))
                {
                    // Try exact match first
                    if (_columnTypes.ContainsKey(sectionName))
                    {
                        Debug.WriteLine($"Column {column.Id}: Found exact match for '{sectionName}'");
                        return _columnTypes[sectionName];
                    }

                    // Try partial match
                    var partialMatch = _columnTypes.Where(kvp => kvp.Key.Contains(sectionName) || sectionName.Contains(kvp.Key)).FirstOrDefault();
                    if (partialMatch.Value != null)
                    {
                        Debug.WriteLine($"Column {column.Id}: Found partial match '{partialMatch.Key}' for '{sectionName}'");
                        return partialMatch.Value;
                    }
                }

                // ADDED: Steel-specific fallback logic before falling back to concrete
                if (frameProps != null && frameProps.Type == FrameMaterialType.Steel)
                {
                    Debug.WriteLine($"Column {column.Id}: No exact/partial match found, trying steel fallback logic");

                    // Step 2: Look for specific steel section type (HSS in this case)
                    var steelFallback = FindSteelColumnBySectionType(frameProps.SteelProps.SectionType);
                    if (steelFallback != null)
                    {
                        Debug.WriteLine($"Column {column.Id}: Found steel section type fallback: {steelFallback.Name}");
                        return steelFallback;
                    }

                    // Step 3: Look for any steel column
                    var anySteelColumn = FindAnySteelColumn();
                    if (anySteelColumn != null)
                    {
                        Debug.WriteLine($"Column {column.Id}: Found generic steel column fallback: {anySteelColumn.Name}");
                        return anySteelColumn;
                    }
                }

                // Step 4: Fallback to concrete column (original behavior)
                Debug.WriteLine($"Column {column.Id}: No specific match found, using fallback concrete column");
                return FindFallbackConcreteColumn();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding column type for {column.Id}: {ex.Message}");
                return FindFallbackConcreteColumn();
            }
        }

        private DB.FamilySymbol FindSteelColumnBySectionType(Core.Models.SteelSectionType sectionType)
        {
            try
            {
                switch (sectionType)
                {
                    case Core.Models.SteelSectionType.W:
                        // Find Wide Flange columns
                        var wSections = _columnTypes.Where(kvp =>
                            kvp.Key.StartsWith("W") ||
                            kvp.Key.Contains("WIDE") ||
                            kvp.Key.Contains("FLANGE"))
                            .ToList();

                        if (wSections.Any())
                        {
                            Debug.WriteLine($"Found W section fallback: {wSections.First().Key}");
                            return wSections.First().Value;
                        }
                        break;

                    case Core.Models.SteelSectionType.HSS:
                        // Find HSS columns
                        var hssSections = _columnTypes.Where(kvp =>
                            kvp.Key.Contains("HSS") ||
                            kvp.Key.Contains("TUBE"))
                            .ToList();

                        if (hssSections.Any())
                        {
                            Debug.WriteLine($"Found HSS section fallback: {hssSections.First().Key}");
                            return hssSections.First().Value;
                        }
                        break;

                    case Core.Models.SteelSectionType.PIPE:
                        // Find Pipe columns
                        var pipeSections = _columnTypes.Where(kvp =>
                            kvp.Key.Contains("PIPE"))
                            .ToList();

                        if (pipeSections.Any())
                        {
                            Debug.WriteLine($"Found PIPE section fallback: {pipeSections.First().Key}");
                            return pipeSections.First().Value;
                        }
                        break;

                    case Core.Models.SteelSectionType.C:
                        // Find Channel columns
                        var cSections = _columnTypes.Where(kvp =>
                            kvp.Key.StartsWith("C") ||
                            kvp.Key.Contains("CHANNEL"))
                            .ToList();

                        if (cSections.Any())
                        {
                            Debug.WriteLine($"Found C section fallback: {cSections.First().Key}");
                            return cSections.First().Value;
                        }
                        break;

                    case Core.Models.SteelSectionType.L:
                        // Find Angle columns
                        var lSections = _columnTypes.Where(kvp =>
                            kvp.Key.StartsWith("L") ||
                            kvp.Key.Contains("ANGLE"))
                            .ToList();

                        if (lSections.Any())
                        {
                            Debug.WriteLine($"Found L section fallback: {lSections.First().Key}");
                            return lSections.First().Value;
                        }
                        break;

                    default:
                        // For other section types, try to find family by section type name
                        var typeSections = _columnTypes.Where(kvp =>
                            kvp.Key.Contains(sectionType.ToString()))
                            .ToList();

                        if (typeSections.Any())
                        {
                            Debug.WriteLine($"Found generic section type fallback: {typeSections.First().Key}");
                            return typeSections.First().Value;
                        }
                        break;
                }

                Debug.WriteLine($"No section-specific steel column found for type: {sectionType}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindSteelColumnBySectionType: {ex.Message}");
                return null;
            }
        }

        private DB.FamilySymbol FindAnySteelColumn()
        {
            try
            {
                // Try to find any steel column
                var steelColumns = _columnTypes.Where(kvp =>
                    kvp.Key.Contains("STEEL") ||
                    kvp.Key.Contains("METAL") ||
                    kvp.Key.StartsWith("W") ||
                    kvp.Key.Contains("HSS") ||
                    kvp.Key.Contains("PIPE") ||
                    kvp.Key.Contains("TUBE"))
                    .ToList();

                if (steelColumns.Any())
                {
                    Debug.WriteLine($"Found generic steel column: {steelColumns.First().Key}");
                    return steelColumns.First().Value;
                }

                Debug.WriteLine("No steel columns found in available types");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindAnySteelColumn: {ex.Message}");
                return null;
            }
        }

        // Log parameters for debugging
        private void LogColumnParameters(DB.FamilyInstance column)
        {
            try
            {
                Debug.WriteLine($"Column parameters for {column.Id}:");
                foreach (DB.Parameter param in column.Parameters)
                {
                    if (param != null && param.Definition != null)
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
                FrameProperties = frameProps,
                Orientation = orientation,
                TopOffset = topOffset
            });
        }

        public int CreateColumns()
        {
            if (_columns.Count == 0)
                return 0;

            Debug.WriteLine($"ColumnImportManager: Creating {_columns.Count} columns");

            // Group columns by location for potential stacking
            var locationGroups = _columns.GroupBy(c => new { X = Math.Round(c.Location.X, 3), Y = Math.Round(c.Location.Y, 3) }).ToList();

            int totalCreated = 0;

            foreach (var locationGroup in locationGroups)
            {
                var columnsAtLocation = locationGroup.OrderBy(c => c.BaseLevel.Elevation).ToList();

                if (CanStackColumns(columnsAtLocation))
                {
                    totalCreated += CreateStackedColumns(columnsAtLocation);
                }
                else
                {
                    totalCreated += CreateIndividualColumns(columnsAtLocation);
                }
            }

            Debug.WriteLine($"ColumnImportManager: Created {totalCreated} columns total");
            return totalCreated;
        }

        // NEW METHOD: Ensures column position is preserved after parameter changes
        private void EnsureColumnPosition(DB.FamilyInstance column, DB.XYZ intendedLocation, string columnId)
        {
            try
            {
                // Get the current location point
                DB.LocationPoint locationPoint = column.Location as DB.LocationPoint;
                if (locationPoint != null)
                {
                    DB.XYZ currentLocation = locationPoint.Point;

                    // Check if the column has moved from intended position
                    double tolerance = 0.01; // 0.01 feet tolerance
                    double deltaX = Math.Abs(currentLocation.X - intendedLocation.X);
                    double deltaY = Math.Abs(currentLocation.Y - intendedLocation.Y);

                    if (deltaX > tolerance || deltaY > tolerance)
                    {
                        Debug.WriteLine($"Column {columnId} position drift detected:");
                        Debug.WriteLine($"  Intended: ({intendedLocation.X:F3}, {intendedLocation.Y:F3})");
                        Debug.WriteLine($"  Current:  ({currentLocation.X:F3}, {currentLocation.Y:F3})");
                        Debug.WriteLine($"  Delta:    ({deltaX:F3}, {deltaY:F3})");

                        // Move the column back to the intended position
                        locationPoint.Point = intendedLocation;

                        Debug.WriteLine($"Column {columnId} position corrected");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring column position for {columnId}: {ex.Message}");
            }
        }

        // Check if columns can be stacked (must be continuous levels)
        private bool CanStackColumns(List<ColumnData> columns)
        {
            if (columns.Count <= 1)
                return false;

            // Sort by base level elevation
            columns = columns.OrderBy(c => c.BaseLevel.Elevation).ToList();

            // Check for continuous levels
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

        // UPDATED METHOD: Create individual columns with position preservation
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

                    // Store the intended location for position verification
                    DB.XYZ intendedLocation = new DB.XYZ(columnData.Location.X, columnData.Location.Y, columnData.Location.Z);

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

                        // CRITICAL FIX: Ensure column position is preserved after parameter changes
                        EnsureColumnPosition(column, intendedLocation, columnData.Id);

                        // Apply rotation if needed
                        ApplyColumnRotation(column, columnData);

                        // Final position check after all modifications
                        EnsureColumnPosition(column, intendedLocation, columnData.Id);

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

                        // Store the intended location for position verification
                        DB.XYZ intendedLocation = new DB.XYZ(bottomColumn.Location.X, bottomColumn.Location.Y, bottomColumn.Location.Z);

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
                            }

                            // CRITICAL FIX: Ensure column position is preserved after parameter changes
                            EnsureColumnPosition(column, intendedLocation, $"Stacked_{bottomColumn.Id}");

                            // Apply rotation
                            ApplyColumnRotation(column, bottomColumn);

                            // Final position check after all modifications
                            EnsureColumnPosition(column, intendedLocation, $"Stacked_{bottomColumn.Id}");

                            // Mark all columns in this stack as created
                            foreach (var stackColumn in stack)
                            {
                                _createdColumns[stackColumn.Id] = column;
                            }

                            count++;
                            Debug.WriteLine($"Created stacked column spanning {stack.Count} levels");
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

        // Find continuous column stacks
        private List<List<ColumnData>> FindContinuousStacks(List<ColumnData> sortedColumns)
        {
            var stacks = new List<List<ColumnData>>();
            var currentStack = new List<ColumnData>();

            foreach (var column in sortedColumns)
            {
                if (currentStack.Count == 0 ||
                    currentStack.Last().TopLevelId.IntegerValue == column.BaseLevelId.IntegerValue)
                {
                    currentStack.Add(column);
                }
                else
                {
                    if (currentStack.Count > 0)
                        stacks.Add(new List<ColumnData>(currentStack));
                    currentStack.Clear();
                    currentStack.Add(column);
                }
            }

            if (currentStack.Count > 0)
                stacks.Add(currentStack);

            return stacks;
        }

        // Apply rotation to column
        private void ApplyColumnRotation(DB.FamilyInstance column, ColumnData columnData)
        {
            try
            {
                if (Math.Abs(columnData.Orientation) > 0.01) // Only rotate if significant
                {
                    DB.LocationPoint locationPoint = column.Location as DB.LocationPoint;
                    if (locationPoint != null)
                    {
                        // Convert degrees to radians
                        double radians = columnData.Orientation * Math.PI / 180.0;

                        // Create vertical axis line through the column's center point
                        DB.XYZ columnCenter = locationPoint.Point;
                        DB.XYZ axisStart = columnCenter;
                        DB.XYZ axisEnd = columnCenter + DB.XYZ.BasisZ; // Vertical axis
                        DB.Line rotationAxis = DB.Line.CreateBound(axisStart, axisEnd);

                        // Rotate around the vertical axis
                        locationPoint.Rotate(rotationAxis, radians);

                        Debug.WriteLine($"Applied {columnData.Orientation}° rotation to column {columnData.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying rotation to column {columnData.Id}: {ex.Message}");
            }
        }
    }

    // Data structure for column information
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
        public FrameProperties FrameProperties { get; set; }
        public double Orientation { get; set; }
        public double TopOffset { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
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
        private static string _logFilePath;

        public ColumnImport(DB.Document doc)
        {
            _doc = doc;
            _columnTypes = LoadColumnTypes();

            // Initialize logger
            InitializeLogger();
        }

        private void InitializeLogger()
        {
            try
            {
                string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitImportLogs");
                Directory.CreateDirectory(logDirectory);
                _logFilePath = Path.Combine(logDirectory, $"ColumnImport_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                LogMessage("=== COLUMN IMPORT LOG STARTED ===");
                LogMessage($"Log file: {_logFilePath}");
                LogMessage($"Document: {_doc.Title}");
                LogMessage($"Timestamp: {DateTime.Now}");
                LogMessage("=" + new string('=', 50));
            }
            catch (Exception ex)
            {
                // Fallback to temp directory
                _logFilePath = Path.Combine(Path.GetTempPath(), $"ColumnImport_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                LogMessage($"Logger initialized with fallback path: {_logFilePath}");
                LogMessage($"Original error: {ex.Message}");
            }
        }

        private void LogMessage(string message)
        {
            try
            {
                if (!string.IsNullOrEmpty(_logFilePath))
                {
                    File.AppendAllText(_logFilePath, $"{DateTime.Now:HH:mm:ss.fff} - {message}\n");
                }
                // Also write to Debug output as backup
                Debug.WriteLine(message);
            }
            catch
            {
                // Silent fail - don't let logging issues break the import
                Debug.WriteLine(message);
            }
        }

        // Initialize dictionary of available column family types
        private Dictionary<string, DB.FamilySymbol> LoadColumnTypes()
        {
            var columnTypes = new Dictionary<string, DB.FamilySymbol>();

            try
            {
                LogMessage("Loading column family types...");

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
                            LogMessage($"Activated column family symbol: {familySymbol.Name}");
                        }
                        catch (Exception activateEx)
                        {
                            LogMessage($"Failed to activate column family symbol {familySymbol.Name}: {activateEx.Message}");
                            continue;
                        }
                    }

                    // Add by symbol name
                    string key = familySymbol.Name.ToUpper();
                    if (!columnTypes.ContainsKey(key))
                    {
                        columnTypes[key] = familySymbol;
                        LogMessage($"Loaded column type: {key} -> {familySymbol.Family.Name}");
                    }

                    // Also add by family name + symbol name for more specific matching
                    string combinedKey = $"{familySymbol.Family.Name}_{familySymbol.Name}".ToUpper();
                    if (!columnTypes.ContainsKey(combinedKey))
                    {
                        columnTypes[combinedKey] = familySymbol;
                        LogMessage($"Loaded column type (combined): {combinedKey}");
                    }
                }

                LogMessage($"Total column types loaded: {columnTypes.Count}");

                if (columnTypes.Count == 0)
                {
                    LogMessage("WARNING: No column types found in the document!");
                }
                else
                {
                    LogMessage("Available column types:");
                    foreach (var kvp in columnTypes.Take(10)) // Log first 10 to avoid spam
                    {
                        LogMessage($"  {kvp.Key} -> {kvp.Value.Family.Name} : {kvp.Value.Name}");
                    }
                    if (columnTypes.Count > 10)
                    {
                        LogMessage($"  ... and {columnTypes.Count - 10} more");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading column types: {ex.Message}");
            }

            return columnTypes;
        }

        // Find appropriate column type based on JSON data and frame properties
        private DB.FamilySymbol FindColumnType(CE.Column jsonColumn, FrameProperties frameProps)
        {
            try
            {
                LogMessage($"\n--- FINDING COLUMN TYPE FOR {jsonColumn.Id} ---");

                // Default to the first column type if we can't find a match
                DB.FamilySymbol defaultType = _columnTypes.Values.FirstOrDefault();

                if (frameProps == null)
                {
                    LogMessage($"Column {jsonColumn.Id}: No frame properties, using default type: {defaultType?.Name}");
                    return defaultType;
                }

                // DEBUG: Log the frame properties details
                LogMessage($"Column {jsonColumn.Id}: Frame Properties Analysis:");
                LogMessage($"  Name: {frameProps.Name}");
                LogMessage($"  Type: {frameProps.Type} (enum value)");
                LogMessage($"  Type == FrameMaterialType.Steel: {frameProps.Type == FrameMaterialType.Steel}");
                LogMessage($"  Type == FrameMaterialType.Concrete: {frameProps.Type == FrameMaterialType.Concrete}");
                LogMessage($"  SteelProps: {(frameProps.SteelProps != null ? $"Present - SectionType: {frameProps.SteelProps.SectionType}, SectionName: {frameProps.SteelProps.SectionName}" : "NULL")}");
                LogMessage($"  ConcreteProps: {(frameProps.ConcreteProps != null ? $"Present - SectionType: {frameProps.ConcreteProps.SectionType}" : "NULL")}");

                // Try to match by name first
                if (!string.IsNullOrEmpty(frameProps.Name))
                {
                    string typeName = frameProps.Name.ToUpper();
                    LogMessage($"  Checking for exact name match: '{typeName}'");

                    if (_columnTypes.TryGetValue(typeName, out DB.FamilySymbol typeByName))
                    {
                        LogMessage($"Column {jsonColumn.Id}: Found exact name match: {typeByName.Name}");
                        return typeByName;
                    }
                    else
                    {
                        LogMessage($"  No exact match found for '{typeName}'");
                    }
                }

                // Enhanced section type matching based on material type and section properties
                if (frameProps.Type == FrameMaterialType.Steel && frameProps.SteelProps != null)
                {
                    LogMessage($"Column {jsonColumn.Id}: Steel column detected, finding steel type");
                    return FindSteelColumnType(frameProps, jsonColumn.Id);
                }
                else if (frameProps.Type == FrameMaterialType.Concrete && frameProps.ConcreteProps != null)
                {
                    LogMessage($"Column {jsonColumn.Id}: Concrete column detected, finding concrete type");
                    return FindOrCreateConcreteColumnType(frameProps);
                }

                // Log why we're falling back to default
                LogMessage($"Column {jsonColumn.Id}: Frame properties don't match steel/concrete criteria - using default");
                LogMessage($"  Condition 1 (Steel): frameProps.Type == FrameMaterialType.Steel = {frameProps.Type == FrameMaterialType.Steel}");
                LogMessage($"  Condition 2 (Steel): frameProps.SteelProps != null = {frameProps.SteelProps != null}");
                LogMessage($"  Condition 3 (Concrete): frameProps.Type == FrameMaterialType.Concrete = {frameProps.Type == FrameMaterialType.Concrete}");
                LogMessage($"  Condition 4 (Concrete): frameProps.ConcreteProps != null = {frameProps.ConcreteProps != null}");
                LogMessage($"  Using default type: {defaultType?.Name}");

                return defaultType;
            }
            catch (Exception ex)
            {
                LogMessage($"Error finding column type for {jsonColumn.Id}: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");
                return _columnTypes.Values.FirstOrDefault();
            }
        }

        private DB.FamilySymbol FindSteelColumnType(FrameProperties frameProps, string columnId)
        {
            LogMessage($"FindSteelColumnType called for column {columnId}");
            LogMessage($"  Section: {frameProps.SteelProps?.SectionName}");

            var sectionType = frameProps.SteelProps.SectionType;
            LogMessage($"  Steel section type: {sectionType}");

            // Attempt to match family by section type
            switch (sectionType)
            {
                case SteelSectionType.HSS:
                    LogMessage("Looking for HSS COLUMN families...");
                    // Find HSS Column families (not beam families)
                    var hssSections = _columnTypes.Where(kvp =>
                        (kvp.Key.Contains("HSS") && kvp.Key.Contains("COLUMN")) ||
                        (kvp.Key.Contains("TUBE") && kvp.Key.Contains("COLUMN")) ||
                        (kvp.Key.Contains("HOLLOW") && kvp.Key.Contains("COLUMN")) ||
                        kvp.Value.Family.Name.ToUpper().Contains("HSS") ||
                        (kvp.Value.Family.Name.ToUpper().Contains("RECTANGULAR") && kvp.Value.Family.Name.ToUpper().Contains("COLUMN")))
                        .ToList();

                    LogMessage($"Found {hssSections.Count} HSS column matches");
                    foreach (var hss in hssSections.Take(5)) // Log first 5
                    {
                        LogMessage($"  HSS Column option: {hss.Key} -> {hss.Value.Family.Name} : {hss.Value.Name}");
                    }

                    if (hssSections.Any())
                    {
                        LogMessage($"Selected HSS Column: {hssSections.First().Value.Name}");
                        return hssSections.First().Value;
                    }
                    break;

                case SteelSectionType.W:
                    LogMessage("Looking for W-section COLUMN families...");
                    // Find Wide Flange Column families (not beam families)
                    var wSections = _columnTypes.Where(kvp =>
                        (kvp.Key.Contains("W") && kvp.Key.Contains("COLUMN")) ||
                        (kvp.Key.Contains("WIDE") && kvp.Key.Contains("COLUMN")) ||
                        (kvp.Key.Contains("FLANGE") && kvp.Key.Contains("COLUMN")) ||
                        kvp.Value.Family.Name.ToUpper().Contains("WIDE-FLANGE") ||
                        (kvp.Value.Family.Name.ToUpper().Contains("W-") && kvp.Value.Family.Name.ToUpper().Contains("COLUMN")))
                        .ToList();

                    LogMessage($"Found {wSections.Count} W-section column matches");
                    foreach (var w in wSections.Take(5)) // Log first 5
                    {
                        LogMessage($"  W Column option: {w.Key} -> {w.Value.Family.Name} : {w.Value.Name}");
                    }

                    if (wSections.Any())
                    {
                        LogMessage($"Selected W-section Column: {wSections.First().Value.Name}");
                        return wSections.First().Value;
                    }
                    break;
            }

            LogMessage("No specific steel column section match, looking for any steel column...");
            // Try to find any steel column as fallback (avoid beam families)
            var steelColumns = _columnTypes.Where(kvp =>
                kvp.Value.Family.Name.ToUpper().Contains("STEEL") ||
                (kvp.Key.Contains("STEEL") && kvp.Key.Contains("COLUMN")) ||
                (kvp.Key.Contains("W") && !kvp.Value.Family.Name.ToUpper().Contains("BEAM")) ||
                (kvp.Key.Contains("HSS") && !kvp.Value.Family.Name.ToUpper().Contains("BEAM")))
                .ToList();

            LogMessage($"Found {steelColumns.Count} general steel column matches");
            foreach (var steel in steelColumns.Take(5))
            {
                LogMessage($"  Steel Column option: {steel.Key} -> {steel.Value.Family.Name} : {steel.Value.Name}");
            }

            if (steelColumns.Any())
            {
                LogMessage($"Selected general steel column: {steelColumns.First().Value.Name}");
                return steelColumns.First().Value;
            }

            LogMessage("No steel columns found, using first available column type");
            var fallback = _columnTypes.Values.FirstOrDefault();
            LogMessage($"Fallback column: {fallback?.Name} from family: {fallback?.Family.Name}");
            return fallback;
        }

        // Rest of the methods remain the same - just adding LogMessage calls where there were Debug.WriteLine calls

        private DB.FamilySymbol FindOrCreateConcreteColumnType(FrameProperties frameProps)
        {
            var sectionType = frameProps.ConcreteProps.SectionType;

            // For rectangular concrete columns, try to create/find with specific dimensions
            if (sectionType == ConcreteSectionType.Rectangular)
            {
                return FindOrCreateRectangularConcreteColumn(frameProps);
            }
            else if (sectionType == ConcreteSectionType.Circular)
            {
                // Find circular columns
                var circColumns = _columnTypes.Where(kvp =>
                    kvp.Key.Contains("CIRC") ||
                    kvp.Key.Contains("ROUND"))
                    .ToList();

                if (circColumns.Any())
                    return circColumns.First().Value;
            }

            // Try to find any concrete column as fallback
            return FindFallbackConcreteColumn();
        }

        private DB.FamilySymbol FindOrCreateRectangularConcreteColumn(FrameProperties frameProps)
        {
            try
            {
                // Get dimensions from frame properties (convert from inches to feet for Revit)
                double widthFeet = frameProps.ConcreteProps.Width / 12.0;
                double depthFeet = frameProps.ConcreteProps.Depth / 12.0;

                // Create expected type name format: "Width x Depth"
                string expectedTypeName = $"{frameProps.ConcreteProps.Width}\" x {frameProps.ConcreteProps.Depth}\"";

                LogMessage($"Looking for concrete column type: {expectedTypeName}");
                LogMessage($"Dimensions: Width={widthFeet:F3}', Depth={depthFeet:F3}'");

                // First, try to find existing type with exact name match
                var exactMatch = _columnTypes.Values.FirstOrDefault(s =>
                    s.Name.Equals(expectedTypeName, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    LogMessage($"Found exact matching column type: {exactMatch.Name}");
                    return exactMatch;
                }

                // Try to find a base rectangular concrete column family to duplicate
                var baseConcreteColumn = FindBaseConcreteRectangularColumn();
                if (baseConcreteColumn != null)
                {
                    LogMessage($"Found base concrete column family: {baseConcreteColumn.Family.Name}");
                    // For now, just return the base type instead of duplicating
                    // Column duplication can be complex and may not always work
                    return baseConcreteColumn;
                }

                LogMessage("No suitable base concrete column family found, using fallback");
                return FindFallbackConcreteColumn();
            }
            catch (Exception ex)
            {
                LogMessage($"Error in FindOrCreateRectangularConcreteColumn: {ex.Message}");
                return FindFallbackConcreteColumn();
            }
        }

        private DB.FamilySymbol FindBaseConcreteRectangularColumn()
        {
            // Look for concrete rectangular column families in order of preference
            string[] preferredNames = {
                "CONCRETE-RECTANGULAR-COLUMN",
                "CONCRETE RECTANGULAR COLUMN",
                "CONC-RECT-COL",
                "RECTANGULAR CONCRETE COLUMN"
            };

            foreach (string preferredName in preferredNames)
            {
                var match = _columnTypes.Values.FirstOrDefault(s =>
                    s.Family.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    LogMessage($"Found preferred concrete column family: {match.Family.Name}");
                    return match;
                }
            }

            // Look for any rectangular concrete column
            var rectConcreteColumn = _columnTypes.Values.FirstOrDefault(s =>
                (s.Family.Name.ToUpper().Contains("CONCRETE") || s.Family.Name.ToUpper().Contains("CONC")) &&
                (s.Family.Name.ToUpper().Contains("RECT") || s.Family.Name.ToUpper().Contains("RECTANGULAR")) &&
                s.Family.Name.ToUpper().Contains("COLUMN"));

            if (rectConcreteColumn != null)
            {
                LogMessage($"Found generic rectangular concrete column: {rectConcreteColumn.Family.Name}");
                return rectConcreteColumn;
            }

            // Last resort - any concrete column
            var anyConcreteColumn = _columnTypes.Values.FirstOrDefault(s =>
                s.Family.Name.ToUpper().Contains("CONCRETE") || s.Family.Name.ToUpper().Contains("CONC"));

            if (anyConcreteColumn != null)
            {
                LogMessage($"Found fallback concrete column: {anyConcreteColumn.Family.Name}");
            }

            return anyConcreteColumn;
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
                LogMessage($"Error getting floor thickness: {ex.Message}");
                return 0; // Return zero thickness on error
            }
        }

        // Imports columns from the JSON model into Revit
        public int Import(List<CE.Column> columns, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;

            LogMessage($"\n=== STARTING COLUMN IMPORT ===");
            LogMessage($"Total columns to import: {columns?.Count ?? 0}");

            if (columns == null || columns.Count == 0)
            {
                LogMessage("No columns to import");
                return 0;
            }

            if (_columnTypes.Count == 0)
            {
                LogMessage("ERROR: No column types available in document!");
                return 0;
            }

            try
            {
                // Create a utility class to group columns
                var columnManager = new ColumnImportManager(_doc, levelIdMap);

                LogMessage("Processing columns...");

                // Process each column from the model
                foreach (var jsonColumn in columns.Take(5)) // Limit to first 5 columns for detailed logging
                {
                    try
                    {
                        LogMessage($"\n--- Processing column {jsonColumn.Id} ---");

                        // Skip if any required data is missing
                        if (string.IsNullOrEmpty(jsonColumn.BaseLevelId) ||
                            string.IsNullOrEmpty(jsonColumn.TopLevelId) ||
                            jsonColumn.StartPoint == null)
                        {
                            LogMessage($"Skipping column {jsonColumn.Id} due to missing data");
                            LogMessage($"  BaseLevelId: {jsonColumn.BaseLevelId ?? "NULL"}");
                            LogMessage($"  TopLevelId: {jsonColumn.TopLevelId ?? "NULL"}");
                            LogMessage($"  StartPoint: {(jsonColumn.StartPoint == null ? "NULL" : "OK")}");
                            continue;
                        }

                        // Get the base and top level ElementIds
                        if (!levelIdMap.TryGetValue(jsonColumn.BaseLevelId, out DB.ElementId baseLevelId) ||
                            !levelIdMap.TryGetValue(jsonColumn.TopLevelId, out DB.ElementId topLevelId))
                        {
                            LogMessage($"Skipping column {jsonColumn.Id} due to missing level mapping");
                            continue;
                        }

                        // Get the levels
                        DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                        DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;
                        if (baseLevel == null || topLevel == null)
                        {
                            LogMessage($"Skipping column {jsonColumn.Id} due to missing levels");
                            continue;
                        }

                        // Get frame properties and find appropriate column type
                        var frameProps = GetFrameProperties(jsonColumn, model);
                        LogMessage($"Frame properties for column {jsonColumn.Id}: {(frameProps != null ? frameProps.Name : "NULL")}");

                        DB.FamilySymbol familySymbol = FindColumnType(jsonColumn, frameProps);

                        if (familySymbol == null)
                        {
                            LogMessage($"Skipping column {jsonColumn.Id} because no suitable family symbol could be found");
                            continue;
                        }

                        LogMessage($"Selected family symbol: {familySymbol.Name} from family: {familySymbol.Family.Name}");

                        // Make sure the family symbol is active
                        if (!familySymbol.IsActive)
                        {
                            try
                            {
                                familySymbol.Activate();
                                LogMessage($"Activated family symbol for column {jsonColumn.Id}: {familySymbol.Name}");
                            }
                            catch (Exception activateEx)
                            {
                                LogMessage($"Error activating family symbol for column {jsonColumn.Id}: {activateEx.Message}");
                                continue;
                            }
                        }

                        // Log completion
                        LogMessage($"Successfully processed column {jsonColumn.Id}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error processing column {jsonColumn.Id}: {ex.Message}");
                    }
                }

                LogMessage($"\n=== COLUMN IMPORT ANALYSIS COMPLETE ===");
                LogMessage($"Log file saved to: {_logFilePath}");
                LogMessage("Check the desktop folder 'RevitImportLogs' for the complete log file.");

                // Continue with the rest of the import process...
                // (Rest of the existing import logic)

            }
            catch (Exception ex)
            {
                LogMessage($"Critical error in column import: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");
            }

            return count;
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

    // Simplified manager class for this logging version
    public class ColumnImportManager
    {
        private readonly DB.Document _doc;
        private readonly Dictionary<string, DB.ElementId> _levelIdMap;

        public ColumnImportManager(DB.Document doc, Dictionary<string, DB.ElementId> levelIdMap)
        {
            _doc = doc;
            _levelIdMap = levelIdMap;
        }
    }
}
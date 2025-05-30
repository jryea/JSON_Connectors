using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Revit.Utilities;
using Autodesk.Revit.DB;

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

            // Try to match by name first
            if (!string.IsNullOrEmpty(frameProps.Name))
            {
                string typeName = frameProps.Name.ToUpper();
                if (_columnTypes.TryGetValue(typeName, out DB.FamilySymbol typeByName))
                {
                    return typeByName;
                }
            }

            // Enhanced section type matching based on material type and section properties
            if (frameProps.Type == FrameMaterialType.Steel && frameProps.SteelProps != null)
            {
                return FindSteelColumnType(frameProps);
            }
            else if (frameProps.Type == FrameMaterialType.Concrete && frameProps.ConcreteProps != null)
            {
                return FindOrCreateConcreteColumnType(frameProps);
            }

            return defaultType;
        }

        private DB.FamilySymbol FindSteelColumnType(Core.Models.Properties.FrameProperties frameProps)
        {
            var sectionType = frameProps.SteelProps.SectionType;

            // Attempt to match family by section type
            switch (sectionType)
            {
                case SteelSectionType.W:
                    // Find Wide Flange columns
                    var wSections = _columnTypes.Where(kvp =>
                        kvp.Key.StartsWith("W") ||
                        kvp.Key.Contains("WIDE") ||
                        kvp.Key.Contains("FLANGE"))
                        .ToList();

                    if (wSections.Any())
                        return wSections.First().Value;
                    break;

                case SteelSectionType.HSS:
                    // Find HSS columns
                    var hssSections = _columnTypes.Where(kvp =>
                        kvp.Key.Contains("HSS") ||
                        kvp.Key.Contains("TUBE"))
                        .ToList();

                    if (hssSections.Any())
                        return hssSections.First().Value;
                    break;

                case SteelSectionType.PIPE:
                    // Find Pipe columns
                    var pipeSections = _columnTypes.Where(kvp =>
                        kvp.Key.Contains("PIPE"))
                        .ToList();

                    if (pipeSections.Any())
                        return pipeSections.First().Value;
                    break;

                default:
                    // For other section types, try to find family by section type name
                    var typeSections = _columnTypes.Where(kvp =>
                        kvp.Key.Contains(sectionType.ToString()))
                        .ToList();

                    if (typeSections.Any())
                        return typeSections.First().Value;
                    break;
            }

            // If still no match, try to find any steel column
            var steelColumns = _columnTypes.Where(kvp =>
                kvp.Key.Contains("STEEL") ||
                kvp.Key.Contains("METAL") ||
                kvp.Key.StartsWith("W") ||
                kvp.Key.Contains("HSS"))
                .ToList();

            return steelColumns.Any() ? steelColumns.First().Value : _columnTypes.Values.FirstOrDefault();
        }

        private DB.FamilySymbol FindOrCreateConcreteColumnType(Core.Models.Properties.FrameProperties frameProps)
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
            var concreteColumns = _columnTypes.Where(kvp =>
                kvp.Key.Contains("CONCRETE") ||
                kvp.Key.Contains("CONC"))
                .ToList();

            return concreteColumns.Any() ? concreteColumns.First().Value : _columnTypes.Values.FirstOrDefault();
        }

        private DB.FamilySymbol FindOrCreateRectangularConcreteColumn(Core.Models.Properties.FrameProperties frameProps)
        {
            try
            {
                // Get dimensions from frame properties (convert from inches to feet for Revit)
                double widthFeet = frameProps.ConcreteProps.Width / 12.0;
                double depthFeet = frameProps.ConcreteProps.Depth / 12.0;

                // Create expected type name format: "Width x Depth"
                string expectedTypeName = $"{frameProps.ConcreteProps.Width}\" x {frameProps.ConcreteProps.Depth}\"";

                Debug.WriteLine($"Looking for concrete column type: {expectedTypeName}");
                Debug.WriteLine($"Dimensions: Width={widthFeet:F3}', Depth={depthFeet:F3}'");

                // First, try to find existing type with exact name match
                var exactMatch = _columnTypes.Values.FirstOrDefault(s =>
                    s.Name.Equals(expectedTypeName, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    Debug.WriteLine($"Found exact matching column type: {exactMatch.Name}");
                    return exactMatch;
                }

                // Try to find a base rectangular concrete column family to duplicate
                var baseConcreteColumn = FindBaseConcreteRectangularColumn();
                if (baseConcreteColumn != null)
                {
                    Debug.WriteLine($"Found base concrete column family: {baseConcreteColumn.Family.Name}");
                    return DuplicateConcreteColumnType(baseConcreteColumn, expectedTypeName, widthFeet, depthFeet);
                }

                Debug.WriteLine("No suitable base concrete column family found, using default");
                return FindFallbackConcreteColumn();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindOrCreateRectangularConcreteColumn: {ex.Message}");
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
                    Debug.WriteLine($"Found preferred concrete column family: {match.Family.Name}");
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
                Debug.WriteLine($"Found generic rectangular concrete column: {rectConcreteColumn.Family.Name}");
                return rectConcreteColumn;
            }

            // Last resort - any concrete column
            var anyConcreteColumn = _columnTypes.Values.FirstOrDefault(s =>
                s.Family.Name.ToUpper().Contains("CONCRETE") || s.Family.Name.ToUpper().Contains("CONC"));

            if (anyConcreteColumn != null)
            {
                Debug.WriteLine($"Found fallback concrete column: {anyConcreteColumn.Family.Name}");
            }

            return anyConcreteColumn;
        }

        private DB.FamilySymbol DuplicateConcreteColumnType(DB.FamilySymbol baseType, string newTypeName, double widthFeet, double depthFeet)
        {
            try
            {
                Debug.WriteLine($"Duplicating column type {baseType.Name} as {newTypeName}");

                // Check if type with this name already exists
                var existingType = _columnTypes.Values.FirstOrDefault(s =>
                    s.Name.Equals(newTypeName, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"Column type {newTypeName} already exists, using existing type");
                    return existingType;
                }

                // Duplicate the column type
                var newType = baseType.Duplicate(newTypeName) as DB.FamilySymbol;
                if (newType == null)
                {
                    Debug.WriteLine("Failed to duplicate column type");
                    return baseType;
                }

                // Activate the new type
                if (!newType.IsActive)
                {
                    newType.Activate();
                }

                // Set the dimensions
                bool dimensionsSet = SetColumnDimensions(newType, widthFeet, depthFeet);

                if (dimensionsSet)
                {
                    // Add to our cache
                    _columnTypes[newType.Name.ToUpper()] = newType;
                    Debug.WriteLine($"Successfully created and cached column type: {newType.Name}");
                    return newType;
                }
                else
                {
                    Debug.WriteLine("Failed to set column dimensions, using original type");
                    return baseType;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error duplicating column type: {ex.Message}");
                return baseType;
            }
        }

        private bool SetColumnDimensions(DB.FamilySymbol columnType, double widthFeet, double depthFeet)
        {
            bool success = false;

            try
            {
                // Common parameter names for rectangular concrete columns
                string[] widthParamNames = { "Width", "b", "B", "Concrete_Width", "Column_Width" };
                string[] depthParamNames = { "Depth", "h", "H", "d", "D", "Concrete_Depth", "Column_Depth" };

                Debug.WriteLine($"Setting column dimensions: Width={widthFeet:F3}', Depth={depthFeet:F3}'");

                // Try to set width parameter
                foreach (string paramName in widthParamNames)
                {
                    var widthParam = columnType.LookupParameter(paramName);
                    if (widthParam != null && !widthParam.IsReadOnly && widthParam.StorageType == StorageType.Double)
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
                    if (depthParam != null && !depthParam.IsReadOnly && depthParam.StorageType == StorageType.Double)
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
                foreach (Parameter param in columnType.Parameters)
                {
                    if (param.StorageType == StorageType.Double)
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

                        // Log orientation information
                        if (jsonColumn.Orientation != 0)
                        {
                            Debug.WriteLine($"Column {jsonColumn.Id} has non-default orientation: {jsonColumn.Orientation} degrees");
                        }

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
                            familySymbol, jsonColumn.IsLateral, frameProps, jsonColumn.Orientation);
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
            private readonly Dictionary<string, DB.FamilyInstance> _createdColumns = new Dictionary<string, DB.FamilyInstance>();
            private readonly List<DB.FamilyInstance> _beamsInModel;
            private const double BEAM_PROXIMITY_TOLERANCE = 1.0; // 1 foot in XY for beam proximity
            private const double ENDPOINT_PROXIMITY_TOLERANCE = 0.5; // 0.5 foot for endpoint proximity

            public ColumnImportManager(DB.Document doc, Dictionary<string, DB.ElementId> levelIdMap)
            {
                _doc = doc;
                _levelIdMap = levelIdMap;

                // Find all beams in the model
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                _beamsInModel = collector.OfClass(typeof(DB.FamilyInstance))
                    .OfCategory(DB.BuiltInCategory.OST_StructuralFraming)
                    .Cast<DB.FamilyInstance>()
                    .Where(f => f.StructuralType == DB.Structure.StructuralType.Beam)
                    .ToList();

                Debug.WriteLine($"Found {_beamsInModel.Count} beams in model for potential column attachment");
            }

            public void AddColumn(string id, DB.XYZ location, DB.Level baseLevel, DB.Level topLevel,
                     DB.FamilySymbol familySymbol, bool isLateral,
                     Core.Models.Properties.FrameProperties frameProps, double orientation = 0)
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
                    Orientation = orientation
                    // TopOffset removed
                });
            }

            // Find a floor above the column's top level for attachment
            private Floor FindFloorAboveColumn(XYZ columnPoint, ElementId columnTopLevelId, List<Floor> allFloors)
            {
                try
                {
                    // Get the column's top level
                    var columnTopLevel = _doc.GetElement(columnTopLevelId) as Level;
                    if (columnTopLevel == null) return null;

                    // Get all levels in the document ordered by elevation
                    var allLevels = new FilteredElementCollector(_doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .OrderBy(l => l.Elevation)
                        .ToList();

                    // Find the next level above the column's top level
                    var nextLevelAbove = allLevels
                        .FirstOrDefault(l => l.Elevation > columnTopLevel.Elevation);

                    if (nextLevelAbove == null)
                    {
                        Debug.WriteLine($"No level found above column top level {columnTopLevel.Name}");
                        return null;
                    }

                    // Look for floors at the level above
                    var floorsAtLevelAbove = allFloors.Where(f =>
                    {
                        var floorLevelId = f.LevelId;
                        return floorLevelId.IntegerValue == nextLevelAbove.Id.IntegerValue;
                    }).ToList();

                    Debug.WriteLine($"Found {floorsAtLevelAbove.Count} floors at level above: {nextLevelAbove.Name}");

                    return floorsAtLevelAbove.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error finding floor above column: {ex.Message}");
                    return null;
                }
            }

            private string GetLocationKey(DB.XYZ location)
            {
                // Use 3 decimal places for position (about 1/8" precision in feet)
                return $"{Math.Round(location.X, 3)}_{Math.Round(location.Y, 3)}";
            }

            public int CreateColumns()
            {
                int count = 0;

                try
                {
                    // First, create a lookup key for each location with sufficient precision
                    var locationGroups = _columns.GroupBy(c => GetLocationKey(c.Location)).ToList();
                    Debug.WriteLine($"Found {locationGroups.Count} column locations after grouping");

                    // Process each unique column location
                    foreach (var locationGroup in locationGroups)
                    {
                        try
                        {
                            // Extract all columns at this location
                            var columnsAtLocation = locationGroup.ToList();
                            DB.XYZ locationPoint = columnsAtLocation[0].Location;
                            Debug.WriteLine($"Processing location {locationPoint.X}, {locationPoint.Y} with {columnsAtLocation.Count} columns");

                            // Group columns by family symbol type
                            var symbolGroups = columnsAtLocation.GroupBy(c => c.FamilySymbol.Id.IntegerValue).ToList();
                            Debug.WriteLine($"Found {symbolGroups.Count} different family symbol types at this location");

                            // Process columns for each family symbol type
                            foreach (var symbolGroup in symbolGroups)
                            {
                                var columnsOfType = symbolGroup.ToList();
                                var familySymbol = columnsOfType[0].FamilySymbol;
                                Debug.WriteLine($"Processing {columnsOfType.Count} columns using family symbol: {familySymbol.Name}");

                                // IMPORTANT CHANGE: Try two different approaches to handle columns
                                bool createAsIndividualColumns = ShouldCreateAsIndividualColumns(columnsOfType);

                                if (createAsIndividualColumns)
                                {
                                    // Create individual columns rather than stacked
                                    count += CreateIndividualColumns(columnsOfType);
                                }
                                else
                                {
                                    // Try to create optimally stacked columns
                                    count += CreateStackedColumns(columnsOfType);
                                }
                            }
                        }
                        catch (Exception locEx)
                        {
                            Debug.WriteLine($"Error processing location group: {locEx.Message}");
                        }
                    }

                    // After creating all columns, process beam attachments
                    AttachColumnsToElements();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Critical error in CreateColumns: {ex.Message}");
                }

                Debug.WriteLine($"Created a total of {count} columns");
                return count;
            }

            // Attach columns to floors and beams on the same level




            // Attach columns to floors and beams on the same level
            private void AttachColumnsToElements()
            {
                try
                {
                    Debug.WriteLine("Beginning column attachment process");
                    int beamAttachmentCount = 0;
                    int floorAttachmentCount = 0;

                    // Get all floors in the model
                    var allFloors = new FilteredElementCollector(_doc)
                        .OfCategory(BuiltInCategory.OST_Floors)
                        .WhereElementIsNotElementType()
                        .Cast<Floor>()
                        .ToList();

                    Debug.WriteLine($"Found {allFloors.Count} floors in the model");

                    // Get unique Revit column instances and group by XY location
                    var uniqueRevitColumns = _createdColumns.Values.Distinct().ToList();
                    Debug.WriteLine($"Found {uniqueRevitColumns.Count} unique Revit column instances");

                    var columnsByLocation = uniqueRevitColumns
                        .GroupBy(col => GetLocationKey(((LocationPoint)col.Location).Point))
                        .ToList();

                    Debug.WriteLine($"Grouped into {columnsByLocation.Count} unique locations");

                    foreach (var locationGroup in columnsByLocation)
                    {
                        var columnsAtLocation = locationGroup.ToList();

                        if (columnsAtLocation.Count == 1)
                        {
                            // Single column at this location - attach normally
                            ProcessColumnAttachment(columnsAtLocation[0], allFloors, ref beamAttachmentCount, ref floorAttachmentCount);
                        }
                        else
                        {
                            // Multiple columns at same XY location - only attach the topmost one
                            Debug.WriteLine($"Found {columnsAtLocation.Count} columns at same location - identifying topmost");

                            var topmostColumn = FindTopmostRevitColumn(columnsAtLocation);
                            if (topmostColumn != null)
                            {
                                Debug.WriteLine($"Attaching only topmost column {topmostColumn.Id} at this location");
                                ProcessColumnAttachment(topmostColumn, allFloors, ref beamAttachmentCount, ref floorAttachmentCount);
                            }
                        }
                    }

                    Debug.WriteLine($"Attachment complete. Attached {beamAttachmentCount} columns to beams and {floorAttachmentCount} columns to floors.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in AttachColumnsToElements: {ex.Message}");
                }
            }


            private FamilyInstance FindTopmostRevitColumn(List<FamilyInstance> columnsAtLocation)
            {
                try
                {
                    FamilyInstance topmostColumn = null;
                    double highestTopElevation = double.MinValue;

                    foreach (var column in columnsAtLocation)
                    {
                        // Get the top level of this Revit column
                        var topLevelParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                        if (topLevelParam == null) continue;

                        var topLevelId = topLevelParam.AsElementId();
                        var topLevel = _doc.GetElement(topLevelId) as Level;
                        if (topLevel == null) continue;

                        // Use only level elevation (no offsets)
                        double actualTopElevation = topLevel.Elevation;

                        Debug.WriteLine($"Revit column {column.Id}: Top level '{topLevel.Name}' at {topLevel.Elevation:F2}");

                        if (actualTopElevation > highestTopElevation)
                        {
                            highestTopElevation = actualTopElevation;
                            topmostColumn = column;
                        }
                    }

                    if (topmostColumn != null)
                    {
                        Debug.WriteLine($"Topmost Revit column identified: {topmostColumn.Id} with top elevation {highestTopElevation:F2}");
                    }

                    return topmostColumn;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error finding topmost Revit column: {ex.Message}");
                    return null;
                }
            }

            private void ProcessColumnAttachment(FamilyInstance column, List<Floor> allFloors, ref int beamAttachmentCount, ref int floorAttachmentCount)
            {
                try
                {
                    // Get column top level
                    var topLevelParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    if (topLevelParam == null) return;

                    var columnTopLevelId = topLevelParam.AsElementId();

                    // Get column location
                    LocationPoint location = column.Location as LocationPoint;
                    if (location == null) return;
                    XYZ columnPoint = location.Point;

                    // Try to attach to a beam first
                    var beamToAttach = FindBeamForColumnAttachment(columnPoint, columnTopLevelId);

                    if (beamToAttach != null)
                    {
                        try
                        {
                            ColumnAttachment.AddColumnAttachment(
                                _doc,
                                column,
                                beamToAttach,
                                1, // 1 = Top attachment
                                ColumnAttachmentCutStyle.CutColumn,
                                ColumnAttachmentJustification.Minimum,
                                0.0 // No offset
                            );

                            beamAttachmentCount++;
                            Debug.WriteLine($"Attached column {column.Id} to beam {beamToAttach.Id}");
                            return; // Skip to next column if beam attachment worked
                        }
                        catch (Exception attachEx)
                        {
                            Debug.WriteLine($"Error attaching column to beam: {attachEx.Message}");
                            // Continue to try floor attachment
                        }
                    }

                    // If beam attachment failed or no beam found, try to attach to floor above
                    var floorToAttach = FindFloorForColumnAttachment(columnPoint, columnTopLevelId, allFloors);

                    if (floorToAttach != null)
                    {
                        try
                        {
                            ColumnAttachment.AddColumnAttachment(
                                _doc,
                                column,
                                floorToAttach,
                                1, // 1 = Top attachment
                                ColumnAttachmentCutStyle.CutColumn,
                                ColumnAttachmentJustification.Minimum,
                                0.0 // No offset
                            );

                            floorAttachmentCount++;
                            Debug.WriteLine($"Attached column {column.Id} to floor {floorToAttach.Id}");
                        }
                        catch (Exception floorAttachEx)
                        {
                            Debug.WriteLine($"Error attaching column to floor: {floorAttachEx.Message}");
                        }
                    }
                }
                catch (Exception colEx)
                {
                    Debug.WriteLine($"Error processing column attachment: {colEx.Message}");
                }
            }


            private FamilyInstance FindBeamForColumnAttachment(XYZ columnPoint, ElementId columnTopLevelId)
            {
                try
                {
                    // Filter beams at this level
                    var beamsAtLevel = _beamsInModel.Where(b =>
                    {
                        var refLevel = b.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId();
                        return refLevel.IntegerValue == columnTopLevelId.IntegerValue;
                    }).ToList();

                    if (beamsAtLevel.Count == 0)
                        return null;

                    // Project column point to XY plane
                    XYZ columnXY = new XYZ(columnPoint.X, columnPoint.Y, 0);

                    // Find beams that the column is under
                    foreach (var beam in beamsAtLevel)
                    {
                        var locationCurve = beam.Location as LocationCurve;
                        if (locationCurve == null) continue;

                        var curve = locationCurve.Curve;
                        if (!(curve is Line)) continue;

                        // Get beam line
                        Line beamLine = curve as Line;
                        var beamStart = beamLine.GetEndPoint(0);
                        var beamEnd = beamLine.GetEndPoint(1);

                        // Project beam points to XY plane
                        XYZ startXY = new XYZ(beamStart.X, beamStart.Y, 0);
                        XYZ endXY = new XYZ(beamEnd.X, beamEnd.Y, 0);

                        // Don't attach if column is at beam endpoint
                        const double ENDPOINT_TOLERANCE = 0.5; // 0.5 feet
                        if (columnXY.DistanceTo(startXY) <= ENDPOINT_TOLERANCE ||
                            columnXY.DistanceTo(endXY) <= ENDPOINT_TOLERANCE)
                        {
                            continue;
                        }

                        // Check if column is under this beam
                        if (IsPointUnderBeamLine(columnXY, startXY, endXY))
                        {
                            return beam;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error finding beam for attachment: {ex.Message}");
                }

                return null;
            }

            // Check if a point is under a beam line
            private bool IsPointUnderBeamLine(XYZ pointXY, XYZ lineStartXY, XYZ lineEndXY)
            {
                try
                {
                    // Create line vector
                    XYZ lineVector = lineEndXY - lineStartXY;
                    double lineLength = lineVector.GetLength();

                    if (lineLength < 0.001) return false;

                    // Normalize line vector
                    XYZ lineDir = lineVector.Normalize();

                    // Vector from line start to point
                    XYZ startToPoint = pointXY - lineStartXY;

                    // Project onto line direction
                    double projection = startToPoint.DotProduct(lineDir);

                    // Check if projection is within line segment
                    if (projection < 0 || projection > lineLength)
                        return false;

                    // Distance from point to line
                    XYZ projectedPoint = lineStartXY + lineDir.Multiply(projection);
                    double distance = pointXY.DistanceTo(projectedPoint);

                    // Return true if point is within tolerance distance of the line
                    const double BEAM_PROXIMITY_TOLERANCE = 1.0; // 1 foot tolerance
                    return distance <= BEAM_PROXIMITY_TOLERANCE;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in IsPointUnderBeamLine: {ex.Message}");
                    return false;
                }
            }

            // Determine if we should use individual columns instead of stacking
            private bool ShouldCreateAsIndividualColumns(List<ColumnData> columns)
            {
                // Criteria for using individual columns:
                // 1. Only one column in the group
                if (columns.Count <= 1)
                    return true;

                // 2. Check for problematic level connections
                // Get all levels in sorted order
                var allLevels = columns.SelectMany(c => new[] { c.BaseLevel, c.TopLevel })
                    .Distinct()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                // Check if we have gaps or overlaps in the column stack
                for (int i = 0; i < columns.Count - 1; i++)
                {
                    var currentCol = columns[i];
                    var nextCol = columns[i + 1];

                    // If the top level of current isn't the base level of next,
                    // there's a discontinuity in the stack
                    if (currentCol.TopLevelId != nextCol.BaseLevelId)
                        return true;
                }

                // 3. If base and top levels of columns form a continuous stack, use stacking
                return false;
            }

            // Create individual columns when stacking isn't appropriate
            private int CreateIndividualColumns(List<ColumnData> columns)
            {
                int count = 0;

                foreach (var columnData in columns)
                {
                    try
                    {
                        // Create column from base to top level
                        DB.FamilyInstance column = _doc.Create.NewFamilyInstance(
                            columnData.Location,
                            columnData.FamilySymbol,
                            columnData.BaseLevel,
                            DB.Structure.StructuralType.Column);

                        if (column == null)
                        {
                            Debug.WriteLine($"Failed to create column {columnData.Id} at {columnData.Location}");
                            continue;
                        }

                        // Set top level only (no offset)
                        try
                        {
                            DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (topLevelParam != null && !topLevelParam.IsReadOnly)
                            {
                                topLevelParam.Set(columnData.TopLevelId);
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
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error creating individual column {columnData.Id}: {ex.Message}");
                    }
                }

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

                            // Set top level only (no offset)
                            try
                            {
                                DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                                if (topLevelParam != null && !topLevelParam.IsReadOnly)
                                {
                                    topLevelParam.Set(topColumn.TopLevelId);
                                }

                                // Apply rotation to the column
                                ApplyColumnRotation(column, bottomColumn);

                                // Record created column for all IDs in the stack
                                foreach (var col in stack)
                                {
                                    _createdColumns[col.Id] = column;
                                }

                                string columnIds = string.Join(", ", stack.Select(c => c.Id));
                                Debug.WriteLine($"Created stacked column from {stack.Count} columns ({columnIds}) from level {bottomColumn.BaseLevel.Name} to {topColumn.TopLevel.Name}");
                                count++;
                            }
                            catch (Exception paramEx)
                            {
                                Debug.WriteLine($"Error setting parameters for stacked column: {paramEx.Message}");
                            }
                        }
                        catch (Exception stackEx)
                        {
                            Debug.WriteLine($"Error creating stacked column: {stackEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in CreateStackedColumns: {ex.Message}");
                }

                return count;
            }


            // Find continuous column stacks from a sorted set of columns
            private List<List<ColumnData>> FindContinuousStacks(List<ColumnData> sortedColumns)
            {
                List<List<ColumnData>> stacks = new List<List<ColumnData>>();
                if (sortedColumns.Count == 0) return stacks;

                List<ColumnData> currentStack = new List<ColumnData>();
                currentStack.Add(sortedColumns[0]);

                for (int i = 1; i < sortedColumns.Count; i++)
                {
                    var prevColumn = sortedColumns[i - 1];
                    var currColumn = sortedColumns[i];

                    // Check for continuity (top of previous = base of current)
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

            // Find a floor for column attachment
            // Find a floor above the column for attachment
            private Floor FindFloorForColumnAttachment(XYZ columnPoint, ElementId columnTopLevelId, List<Floor> allFloors)
            {
                try
                {
                    // Get the column's top level
                    var columnTopLevel = _doc.GetElement(columnTopLevelId) as Level;
                    if (columnTopLevel == null) return null;

                    // Get all levels in the document ordered by elevation
                    var allLevels = new FilteredElementCollector(_doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .OrderBy(l => l.Elevation)
                        .ToList();

                    // Find the next level above the column's top level
                    var nextLevelAbove = allLevels
                        .FirstOrDefault(l => l.Elevation > columnTopLevel.Elevation);

                    if (nextLevelAbove == null)
                    {
                        Debug.WriteLine($"No level found above column top level {columnTopLevel.Name}");
                        return null;
                    }

                    // Look for floors at the level above
                    var floorsAtLevelAbove = allFloors.Where(f =>
                    {
                        var floorLevelId = f.LevelId;
                        return floorLevelId.IntegerValue == nextLevelAbove.Id.IntegerValue;
                    }).ToList();

                    Debug.WriteLine($"Found {floorsAtLevelAbove.Count} floors at level above: {nextLevelAbove.Name}");

                    return floorsAtLevelAbove.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error finding floor above column: {ex.Message}");
                    return null;
                }
            }


            // Apply rotation to a column
            private void ApplyColumnRotation(DB.FamilyInstance column, ColumnData columnData)
            {
                try
                {
                    if (columnData.Orientation == 0)
                        return; // No rotation needed

                    // Create rotation axis (vertical line at column location)
                    DB.XYZ basePoint = columnData.Location;
                    DB.XYZ topPoint = new DB.XYZ(basePoint.X, basePoint.Y, basePoint.Z + 1.0);
                    DB.Line rotationAxis = DB.Line.CreateBound(basePoint, topPoint);

                    // Convert orientation to radians - use 90 degree offset to match Revit conventions
                    double rotationAngle = (90.0 + columnData.Orientation) * Math.PI / 180.0;

                    // Apply rotation
                    DB.ElementTransformUtils.RotateElement(
                        _doc,
                        column.Id,
                        rotationAxis,
                        rotationAngle
                    );

                    Debug.WriteLine($"Applied rotation of {columnData.Orientation} degrees to column");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error applying column rotation: {ex.Message}");
                }
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
            public double Orientation { get; set; } = 0;
            public Core.Models.Properties.FrameProperties FrameProps { get; set; }
            // TopOffset removed - no longer used
            public double X => Location.X;
            public double Y => Location.Y;
        }
    }
}
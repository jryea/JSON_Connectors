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

            // Try to match by name
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

                if (steelColumns.Any())
                    return steelColumns.First().Value;
            }
            else if (frameProps.Type == FrameMaterialType.Concrete && frameProps.ConcreteProps != null)
            {
                var sectionType = frameProps.ConcreteProps.SectionType;

                // Attempt to match concrete column by section type
                switch (sectionType)
                {
                    case ConcreteSectionType.Rectangular:
                        var rectColumns = _columnTypes.Where(kvp =>
                            kvp.Key.Contains("RECT") ||
                            kvp.Key.Contains("SQUARE") ||
                            (kvp.Key.Contains("CONCRETE") && !kvp.Key.Contains("ROUND")))
                            .ToList();

                        if (rectColumns.Any())
                            return rectColumns.First().Value;
                        break;

                    case ConcreteSectionType.Circular:
                        var circColumns = _columnTypes.Where(kvp =>
                            kvp.Key.Contains("CIRC") ||
                            kvp.Key.Contains("ROUND"))
                            .ToList();

                        if (circColumns.Any())
                            return circColumns.First().Value;
                        break;

                    default:
                        // Try to find any concrete column
                        var concreteColumns = _columnTypes.Where(kvp =>
                            kvp.Key.Contains("CONCRETE") ||
                            kvp.Key.Contains("CONC"))
                            .ToList();

                        if (concreteColumns.Any())
                            return concreteColumns.First().Value;
                        break;
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
                                              familySymbol, jsonColumn.IsLateral, frameProps, topOffset, jsonColumn.Orientation);
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
                                 Core.Models.Properties.FrameProperties frameProps, double topOffset, double orientation = 0)
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
                    TopOffset = topOffset,
                    Orientation = orientation
                });
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

                    foreach (var columnEntry in _createdColumns)
                    {
                        var columnId = columnEntry.Key;
                        var column = columnEntry.Value;

                        try
                        {
                            // Get column top level
                            var topLevelParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (topLevelParam == null) continue;

                            var columnTopLevelId = topLevelParam.AsElementId();

                            // Get column location
                            LocationPoint location = column.Location as LocationPoint;
                            if (location == null) continue;
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
                                    Debug.WriteLine($"Attached column {columnId} to beam {beamToAttach.Id}");
                                    continue; // Skip to next column if beam attachment worked
                                }
                                catch (Exception attachEx)
                                {
                                    Debug.WriteLine($"Error attaching column to beam: {attachEx.Message}");
                                    // Continue to try floor attachment
                                }
                            }

                            // If beam attachment failed or no beam found, try to attach to floor
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
                                    Debug.WriteLine($"Attached column {columnId} to floor {floorToAttach.Id}");
                                }
                                catch (Exception floorAttachEx)
                                {
                                    Debug.WriteLine($"Error attaching column to floor: {floorAttachEx.Message}");
                                }
                            }
                        }
                        catch (Exception colEx)
                        {
                            Debug.WriteLine($"Error processing column {columnId} for attachment: {colEx.Message}");
                        }
                    }

                    Debug.WriteLine($"Attachment complete. Attached {beamAttachmentCount} columns to beams and {floorAttachmentCount} columns to floors.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in AttachColumnsToElements: {ex.Message}");
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

                        // Set top level
                        try
                        {
                            DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (topLevelParam != null && !topLevelParam.IsReadOnly)
                            {
                                topLevelParam.Set(columnData.TopLevelId);
                            }

                            // Set top offset
                            DB.Parameter topOffsetParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                            {
                                topOffsetParam.Set(columnData.TopOffset);
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

                            // Set top level
                            try
                            {
                                DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                                if (topLevelParam != null && !topLevelParam.IsReadOnly)
                                {
                                    topLevelParam.Set(topColumn.TopLevelId);
                                }

                                // Set top offset
                                DB.Parameter topOffsetParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                                if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                                {
                                    topOffsetParam.Set(topColumn.TopOffset);
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
            private Floor FindFloorForColumnAttachment(XYZ columnPoint, ElementId columnTopLevelId, List<Floor> allFloors)
            {
                try
                {
                    // Filter floors at this level
                    var floorsAtLevel = allFloors.Where(f =>
                    {
                        var floorLevelId = f.LevelId;
                        return floorLevelId.IntegerValue == columnTopLevelId.IntegerValue;
                    }).ToList();

                    if (floorsAtLevel.Count == 0)
                        return null;

                    // Option 1: Just use the first floor on this level (as requested in fallback)
                    return floorsAtLevel.FirstOrDefault();

                    // Option 2: Check if column is inside floor boundary
                    // Uncomment this section if you want to implement the point-in-polygon check
                    /*
                    foreach (var floor in floorsAtLevel)
                    {
                        if (IsPointWithinFloor(columnPoint, floor))
                        {
                            return floor;
                        }
                    }
                    */
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error finding floor for attachment: {ex.Message}");
                }

                return null;
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
            public double TopOffset { get; set; } = 0;
            public double X => Location.X;
            public double Y => Location.Y;
        }
    }
}
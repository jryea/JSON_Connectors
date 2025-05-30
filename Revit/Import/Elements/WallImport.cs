using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models;
using CE = Core.Models.Elements;
using Revit.Utilities;
using System.Diagnostics;

namespace Revit.Import.Elements
{
    public class WallImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.ElementId> _wallTypeCache;
        private Dictionary<string, double> _levelFloorThicknessCache;
        private DB.WallType _defaultWallType;
        private List<DB.WallType> _concreteWallTypes;

        public WallImport(DB.Document doc)
        {
            _doc = doc;
            _wallTypeCache = new Dictionary<string, DB.ElementId>();
            _levelFloorThicknessCache = new Dictionary<string, double>();
            InitializeWallTypes();
        }

        private void InitializeWallTypes()
        {
            // Get available wall types from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.WallType));
            List<DB.WallType> wallTypes = collector.Cast<DB.WallType>()
                .Where(wt => wt.Kind == DB.WallKind.Basic)
                .ToList();

            if (wallTypes.Count == 0)
            {
                Debug.WriteLine("No basic wall types found in the document");
                return;
            }

            // Get IMEG concrete wall types
            _concreteWallTypes = wallTypes
                .Where(wt => wt.Name.Contains("IMEG_Concrete"))
                .ToList();

            // Set default wall type (prefer IMEG_Concrete walls)
            _defaultWallType = _concreteWallTypes.FirstOrDefault() ??
                wallTypes.FirstOrDefault(wt => wt.Name.Contains("Concrete")) ??
                wallTypes.FirstOrDefault();

            Debug.WriteLine($"Default wall type: {(_defaultWallType?.Name ?? "None")}");
            Debug.WriteLine($"Found {_concreteWallTypes.Count} IMEG_Concrete wall types");
        }

        public int Import(List<CE.Wall> walls, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;

            if (walls == null || walls.Count == 0 || _defaultWallType == null)
                return 0;

            // Pre-calculate floor thicknesses for each level
            CalculateFloorThicknesses(model);

            try
            {
                // Create a utility class to group walls for stacking
                var wallManager = new WallImportManager(_doc, levelIdMap);

                // Process each wall from the model
                foreach (var jsonWall in walls)
                {
                    try
                    {
                        // Skip walls with less than 2 points
                        if (jsonWall.Points == null || jsonWall.Points.Count < 2)
                            continue;

                        // Get base and top levels
                        if (!levelIdMap.TryGetValue(jsonWall.BaseLevelId, out DB.ElementId baseLevelId) ||
                            !levelIdMap.TryGetValue(jsonWall.TopLevelId, out DB.ElementId topLevelId))
                            continue;

                        // Get the Levels from the IDs
                        DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                        DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;
                        if (baseLevel == null || topLevel == null)
                            continue;

                        // Skip if top level isn't higher than base level
                        if (topLevel.Elevation <= baseLevel.Elevation)
                        {
                            Debug.WriteLine($"Skipping wall {jsonWall.Id} because top level elevation is not higher than base level elevation");
                            continue;
                        }

                        // Get appropriate wall type
                        DB.ElementId wallTypeId = GetWallTypeId(jsonWall, model);

                        // Create the wall curve
                        DB.Curve wallCurve = CreateWallCurve(jsonWall);
                        if (wallCurve == null)
                            continue;

                        // Calculate top offset based on floor thickness at the top level
                        double topOffset = GetTopOffset(jsonWall.TopLevelId);

                        // Add to wall manager for stacking
                        wallManager.AddWall(jsonWall.Id, wallCurve, baseLevel, topLevel,
                                          wallTypeId, jsonWall.PropertiesId, topOffset);
                    }
                    catch (Exception wallEx)
                    {
                        Debug.WriteLine($"Error processing wall {jsonWall.Id}: {wallEx.Message}");
                    }
                }

                // Create the stacked walls
                count = wallManager.CreateWalls();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical error in wall import: {ex.Message}");
            }

            return count;
        }

        private void CalculateFloorThicknesses(BaseModel model)
        {
            _levelFloorThicknessCache.Clear();

            // Get floors from model
            var floors = model.Elements.Floors;
            if (floors == null || floors.Count == 0)
                return;

            // Calculate floor thickness for each level
            foreach (var floor in floors)
            {
                if (string.IsNullOrEmpty(floor.LevelId) || string.IsNullOrEmpty(floor.FloorPropertiesId))
                    continue;

                // Get floor property
                var floorProp = model.Properties.FloorProperties
                    .FirstOrDefault(fp => fp.Id == floor.FloorPropertiesId);

                if (floorProp == null)
                    continue;

                // Store floor thickness for this level (in feet)
                double thicknessInFeet = floorProp.Thickness / 12.0;
                _levelFloorThicknessCache[floor.LevelId] = thicknessInFeet;

                Debug.WriteLine($"Cached floor thickness for level {floor.LevelId}: {thicknessInFeet}ft");
            }
        }

        private double GetTopOffset(string topLevelId)
        {
            // Check if we have a cached floor thickness for this level
            if (_levelFloorThicknessCache.TryGetValue(topLevelId, out double floorThickness))
            {
                // Apply negative offset (to position wall below the floor)
                return -floorThickness;
            }
            return 0.0;
        }

        private DB.Curve CreateWallCurve(CE.Wall jsonWall)
        {
            try
            {
                // Create a line from first two points
                return Helpers.CreateRevitCurve(jsonWall.Points[0], jsonWall.Points[1]);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating wall curve: {ex.Message}");
                return null;
            }
        }

        private DB.ElementId GetWallTypeId(CE.Wall jsonWall, BaseModel model)
        {
            // Check if already cached
            string cacheKey = jsonWall.PropertiesId ?? "default";
            if (_wallTypeCache.TryGetValue(cacheKey, out DB.ElementId cachedId))
                return cachedId;

            // Default to use if nothing else works
            DB.ElementId wallTypeId = _defaultWallType.Id;

            if (!string.IsNullOrEmpty(jsonWall.PropertiesId))
            {
                var jsonWallProp = model.Properties.WallProperties
                    .FirstOrDefault(wp => wp.Id == jsonWall.PropertiesId);

                if (jsonWallProp != null)
                {
                    // Get wall thickness in inches
                    double thicknessInches = jsonWallProp.Thickness;

                    // Format the name as expected in the template: "IMEG_Concrete {thickness}""
                    string expectedWallTypeName = $"IMEG_Concrete {thicknessInches}\"";

                    // Try to find matching wall type by name
                    var matchedWallType = _concreteWallTypes
                        .FirstOrDefault(wt => wt.Name.Equals(expectedWallTypeName, StringComparison.OrdinalIgnoreCase));

                    if (matchedWallType != null)
                    {
                        wallTypeId = matchedWallType.Id;
                        Debug.WriteLine($"Found exact matching wall type: {matchedWallType.Name}");
                    }
                    else
                    {
                        // Try to duplicate existing wall type and adjust thickness
                        var newWallType = CreateWallTypeWithThickness(thicknessInches, expectedWallTypeName);
                        if (newWallType != null)
                        {
                            wallTypeId = newWallType.Id;
                            Debug.WriteLine($"Created new wall type: {newWallType.Name}");
                        }
                    }
                }
            }

            // Cache the result
            _wallTypeCache[cacheKey] = wallTypeId;
            return wallTypeId;
        }

        private DB.WallType CreateWallTypeWithThickness(double thicknessInches, string newTypeName)
        {
            try
            {
                // Check if the type already exists
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                var existingType = collector.OfClass(typeof(DB.WallType))
                    .Cast<DB.WallType>()
                    .FirstOrDefault(wt => wt.Name.Equals(newTypeName, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"Wall type '{newTypeName}' already exists");
                    return existingType;
                }

                // Find a base type to duplicate - prefer IMEG concrete walls
                DB.WallType baseWallType = _concreteWallTypes.FirstOrDefault() ?? _defaultWallType;

                // Duplicate the wall type
                DB.WallType newWallType = baseWallType.Duplicate(newTypeName) as DB.WallType;
                if (newWallType == null)
                    return baseWallType;

                // Get the compound structure
                DB.CompoundStructure cs = newWallType.GetCompoundStructure();
                if (cs == null)
                    return newWallType;

                // Convert inches to feet for Revit
                double thicknessInFeet = thicknessInches / 12.0;

                // Assuming single layer wall - set the thickness directly
                cs.SetLayerWidth(0, thicknessInFeet);
                newWallType.SetCompoundStructure(cs);
                Debug.WriteLine($"Created wall type with thickness {thicknessInFeet}ft");
                return newWallType;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating wall type: {ex.Message}");
                return _defaultWallType;
            }
        }

        // Utility class to manage wall stacking (similar to ColumnImportManager)
        private class WallImportManager
        {
            private readonly DB.Document _doc;
            private readonly Dictionary<string, DB.ElementId> _levelIdMap;
            private readonly List<WallData> _walls = new List<WallData>();
            private readonly Dictionary<string, DB.Wall> _createdWalls = new Dictionary<string, DB.Wall>();

            public WallImportManager(DB.Document doc, Dictionary<string, DB.ElementId> levelIdMap)
            {
                _doc = doc;
                _levelIdMap = levelIdMap;
            }

            public void AddWall(string id, DB.Curve curve, DB.Level baseLevel, DB.Level topLevel,
                               DB.ElementId wallTypeId, string propertiesId, double topOffset)
            {
                _walls.Add(new WallData
                {
                    Id = id,
                    Curve = curve,
                    BaseLevel = baseLevel,
                    TopLevel = topLevel,
                    BaseLevelId = baseLevel.Id,
                    TopLevelId = topLevel.Id,
                    WallTypeId = wallTypeId,
                    PropertiesId = propertiesId,
                    TopOffset = topOffset
                });
            }

            public int CreateWalls()
            {
                int count = 0;

                try
                {
                    // Group walls by location and properties
                    var locationGroups = _walls.GroupBy(w => GetWallLocationKey(w)).ToList();
                    Debug.WriteLine($"Found {locationGroups.Count} wall locations after grouping");

                    // Process each unique wall location
                    foreach (var locationGroup in locationGroups)
                    {
                        try
                        {
                            var wallsAtLocation = locationGroup.ToList();
                            Debug.WriteLine($"Processing location with {wallsAtLocation.Count} walls");

                            // Group by wall type and properties
                            var propertyGroups = wallsAtLocation.GroupBy(w => $"{w.WallTypeId.IntegerValue}_{w.PropertiesId ?? ""}").ToList();
                            Debug.WriteLine($"Found {propertyGroups.Count} different wall property types at this location");

                            // Process walls for each property group
                            foreach (var propertyGroup in propertyGroups)
                            {
                                var wallsOfType = propertyGroup.ToList();
                                Debug.WriteLine($"Processing {wallsOfType.Count} walls with same properties");

                                // Determine if we should create stacked or individual walls
                                bool createAsStackedWall = ShouldCreateAsStackedWall(wallsOfType);

                                if (createAsStackedWall)
                                {
                                    count += CreateStackedWalls(wallsOfType);
                                }
                                else
                                {
                                    count += CreateIndividualWalls(wallsOfType);
                                }
                            }
                        }
                        catch (Exception locEx)
                        {
                            Debug.WriteLine($"Error processing wall location group: {locEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Critical error in CreateWalls: {ex.Message}");
                }

                Debug.WriteLine($"Created a total of {count} walls");
                return count;
            }

            private string GetWallLocationKey(WallData wall)
            {
                // Create a key based on wall curve endpoints with sufficient precision
                if (wall.Curve is DB.Line line)
                {
                    var start = line.GetEndPoint(0);
                    var end = line.GetEndPoint(1);

                    // Normalize line direction (always start with lower coordinate point)
                    if (start.X > end.X || (Math.Abs(start.X - end.X) < 0.001 && start.Y > end.Y))
                    {
                        var temp = start;
                        start = end;
                        end = temp;
                    }

                    return $"{Math.Round(start.X, 3)}_{Math.Round(start.Y, 3)}_{Math.Round(end.X, 3)}_{Math.Round(end.Y, 3)}";
                }

                return wall.Id; // Fallback for non-linear walls
            }

            private bool ShouldCreateAsStackedWall(List<WallData> walls)
            {
                // Criteria for using stacked walls:
                // 1. More than one wall in the group
                if (walls.Count <= 1)
                    return false;

                // 2. Check if walls form a continuous vertical stack
                return FormsStackedSequence(walls);
            }

            private bool FormsStackedSequence(List<WallData> walls)
            {
                // Sort walls by base level elevation
                var sortedWalls = walls.OrderBy(w => w.BaseLevel.Elevation).ToList();

                // Check for continuity (top of previous = base of current)
                for (int i = 0; i < sortedWalls.Count - 1; i++)
                {
                    var currentWall = sortedWalls[i];
                    var nextWall = sortedWalls[i + 1];

                    // Check if top level of current wall equals base level of next wall
                    if (currentWall.TopLevelId.IntegerValue != nextWall.BaseLevelId.IntegerValue)
                    {
                        Debug.WriteLine($"Gap found between walls: {currentWall.TopLevel.Name} -> {nextWall.BaseLevel.Name}");
                        return false;
                    }
                }

                Debug.WriteLine($"Walls form continuous stack from {sortedWalls.First().BaseLevel.Name} to {sortedWalls.Last().TopLevel.Name}");
                return true;
            }

            private int CreateStackedWalls(List<WallData> walls)
            {
                try
                {
                    // Sort walls by base level elevation
                    var sortedWalls = walls.OrderBy(w => w.BaseLevel.Elevation).ToList();
                    var bottomWall = sortedWalls.First();
                    var topWall = sortedWalls.Last();

                    Debug.WriteLine($"Creating stacked wall from {bottomWall.BaseLevel.Name} to {topWall.TopLevel.Name}");

                    // Calculate wall height as difference between base and top levels
                    double baseElevation = bottomWall.BaseLevel.Elevation;
                    double topElevation = topWall.TopLevel.Elevation;
                    double wallHeight = topElevation - baseElevation;

                    // Check if wall height is valid
                    if (wallHeight <= 0)
                    {
                        Debug.WriteLine($"Invalid wall height for stacked wall: {wallHeight}");
                        return 0;
                    }

                    // Create the wall using the bottom wall's curve and base level
                    DB.Wall wall = DB.Wall.Create(
                        _doc,
                        bottomWall.Curve,
                        bottomWall.WallTypeId,
                        bottomWall.BaseLevelId,
                        wallHeight,
                        0.0, // Offset
                        false, // Flip
                        true); // Structural

                    if (wall == null)
                    {
                        Debug.WriteLine("Failed to create stacked wall");
                        return 0;
                    }

                    // Set top level constraint
                    wall.get_Parameter(DB.BuiltInParameter.WALL_HEIGHT_TYPE).Set(topWall.TopLevelId);

                    // Apply final top offset from the top wall
                    if (Math.Abs(topWall.TopOffset) > 0.001)
                    {
                        var topOffsetParam = wall.get_Parameter(DB.BuiltInParameter.WALL_TOP_OFFSET);
                        if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                        {
                            topOffsetParam.Set(topWall.TopOffset);
                            Debug.WriteLine($"Set stacked wall top offset to {topWall.TopOffset}ft");
                        }
                    }

                    // Record created wall for all IDs in the stack
                    foreach (var wallData in sortedWalls)
                    {
                        _createdWalls[wallData.Id] = wall;
                    }

                    string wallIds = string.Join(", ", sortedWalls.Select(w => w.Id));
                    Debug.WriteLine($"Created stacked wall from {sortedWalls.Count} walls ({wallIds})");

                    return 1; // One stacked wall created
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating stacked wall: {ex.Message}");
                    return 0;
                }
            }

            private int CreateIndividualWalls(List<WallData> walls)
            {
                int count = 0;

                foreach (var wallData in walls)
                {
                    try
                    {
                        // Calculate wall height as difference between base and top levels
                        double baseElevation = wallData.BaseLevel.Elevation;
                        double topElevation = wallData.TopLevel.Elevation;
                        double wallHeight = topElevation - baseElevation;

                        // Check if wall height is valid
                        if (wallHeight <= 0)
                        {
                            Debug.WriteLine($"Invalid wall height for wall {wallData.Id}: {wallHeight}");
                            continue;
                        }

                        // Create the wall
                        DB.Wall wall = DB.Wall.Create(
                            _doc,
                            wallData.Curve,
                            wallData.WallTypeId,
                            wallData.BaseLevelId,
                            wallHeight,
                            0.0, // Offset
                            false, // Flip
                            true); // Structural

                        if (wall == null)
                        {
                            Debug.WriteLine($"Failed to create wall {wallData.Id}");
                            continue;
                        }

                        // Set top level constraint
                        wall.get_Parameter(DB.BuiltInParameter.WALL_HEIGHT_TYPE).Set(wallData.TopLevelId);

                        // Apply top offset
                        if (Math.Abs(wallData.TopOffset) > 0.001)
                        {
                            var topOffsetParam = wall.get_Parameter(DB.BuiltInParameter.WALL_TOP_OFFSET);
                            if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                            {
                                topOffsetParam.Set(wallData.TopOffset);
                                Debug.WriteLine($"Set wall top offset to {wallData.TopOffset}ft");
                            }
                        }

                        Debug.WriteLine($"Created individual wall {wallData.Id} from {wallData.BaseLevel.Name} to {wallData.TopLevel.Name}");
                        _createdWalls[wallData.Id] = wall;
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error creating individual wall {wallData.Id}: {ex.Message}");
                    }
                }

                return count;
            }
        }

        private class WallData
        {
            public string Id { get; set; }
            public DB.Curve Curve { get; set; }
            public DB.Level BaseLevel { get; set; }
            public DB.Level TopLevel { get; set; }
            public DB.ElementId BaseLevelId { get; set; }
            public DB.ElementId TopLevelId { get; set; }
            public DB.ElementId WallTypeId { get; set; }
            public string PropertiesId { get; set; }
            public double TopOffset { get; set; } = 0;
        }
    }
}
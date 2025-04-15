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

                    // Get appropriate wall type
                    DB.ElementId wallTypeId = GetWallTypeId(jsonWall, model);

                    // Create the wall curve
                    DB.Curve wallCurve = CreateWallCurve(jsonWall);
                    if (wallCurve == null)
                        continue;

                    // Calculate wall height as difference between base and top levels
                    double baseElevation = baseLevel.Elevation;
                    double topElevation = topLevel.Elevation;
                    double wallHeight = topElevation - baseElevation;

                    // Check if wall height is valid
                    if (wallHeight <= 0)
                    {
                        Debug.WriteLine($"Invalid wall height for wall {jsonWall.Id}: {wallHeight}");
                        continue;
                    }

                    // Create the wall
                    DB.Wall wall = DB.Wall.Create(
                        _doc,
                        wallCurve,
                        wallTypeId,
                        baseLevelId,
                        wallHeight,
                        0.0, // Offset
                        false, // Flip
                        true); // Structural

                    if (wall == null)
                        continue;

                    // Set top level constraint
                    wall.get_Parameter(DB.BuiltInParameter.WALL_HEIGHT_TYPE).Set(topLevelId);

                    // Apply top offset based on floor thickness
                    ApplyTopOffset(wall, jsonWall.TopLevelId);

                    count++;
                    Debug.WriteLine($"Created wall with type '{_doc.GetElement(wallTypeId).Name}' from {baseLevel.Name} to {topLevel.Name}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating wall {jsonWall.Id}: {ex.Message}");
                }
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

        private void ApplyTopOffset(DB.Wall wall, string topLevelId)
        {
            try
            {
                // Get top offset parameter
                DB.Parameter topOffsetParam = wall.get_Parameter(DB.BuiltInParameter.WALL_TOP_OFFSET);
                if (topOffsetParam == null || topOffsetParam.IsReadOnly)
                    return;

                // Check if we have a cached floor thickness for this level
                if (_levelFloorThicknessCache.TryGetValue(topLevelId, out double floorThickness))
                {
                    // Apply negative offset (to position wall below the floor)
                    double offset = -floorThickness;
                    topOffsetParam.Set(offset);
                    Debug.WriteLine($"Set wall top offset to {offset}ft based on floor thickness");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting wall top offset: {ex.Message}");
            }
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
    }
}
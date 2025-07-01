using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Core.Models.Properties;
using Revit.Utilities;

namespace Revit.Import.Elements
{
    // Imports brace elements from JSON into Revit
    public class BraceImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FamilySymbol> _braceTypes;

        public BraceImport(DB.Document doc)
        {
            _doc = doc;
            InitializeBraceTypes();
        }

        // Initialize dictionary of available brace family types
        private void InitializeBraceTypes()
        {
            _braceTypes = new Dictionary<string, DB.FamilySymbol>();

            // Get all structural framing family symbols that could be braces
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.FamilySymbol));
            collector.OfCategory(DB.BuiltInCategory.OST_StructuralFraming);

            foreach (DB.FamilySymbol symbol in collector)
            {
                if (!symbol.IsActive)
                {
                    try { symbol.Activate(); }
                    catch { continue; }
                }

                string key = symbol.Name.ToUpper();

                // Try to identify brace types
                bool isBrace = symbol.Family.Name.ToUpper().Contains("BRACE") ||
                               key.Contains("BRACE");

                if (isBrace)
                {
                    if (!_braceTypes.ContainsKey(key))
                    {
                        _braceTypes[key] = symbol;
                    }
                }

                // Also add by family name + symbol name for more specific matching
                string combinedKey = $"{symbol.Family.Name}_{symbol.Name}".ToUpper();
                if (isBrace)
                {
                    if (!_braceTypes.ContainsKey(combinedKey))
                    {
                        _braceTypes[combinedKey] = symbol;
                    }
                }
            }

            Debug.WriteLine($"Loaded {_braceTypes.Count} brace family types");

            // If no brace types were found specifically, use beam types as fallback
            if (_braceTypes.Count == 0)
            {
                // Get all structural framing types for fallback
                collector = new DB.FilteredElementCollector(_doc);
                collector.OfClass(typeof(DB.FamilySymbol));
                collector.OfCategory(DB.BuiltInCategory.OST_StructuralFraming);

                foreach (DB.FamilySymbol symbol in collector)
                {
                    if (!symbol.IsActive)
                    {
                        try { symbol.Activate(); }
                        catch { continue; }
                    }

                    string key = symbol.Name.ToUpper();
                    if (!_braceTypes.ContainsKey(key))
                    {
                        _braceTypes[key] = symbol;
                    }
                }

                Debug.WriteLine($"Using fallback: Loaded {_braceTypes.Count} structural framing types for braces");
            }
        }

        // Find appropriate brace type based on frame properties
        private DB.FamilySymbol FindBraceType(FrameProperties frameProps)
        {
            if (frameProps == null || string.IsNullOrEmpty(frameProps.Name))
            {
                // Return a default if available
                if (_braceTypes.Count > 0)
                {
                    var defaultSymbol = _braceTypes.Values.First();
                    Debug.WriteLine($"Using default brace type: {defaultSymbol.Name}");
                    return defaultSymbol;
                }
                return null;
            }

            string searchName = frameProps.Name.ToUpper();

            // First try exact match
            if (_braceTypes.ContainsKey(searchName))
            {
                Debug.WriteLine($"Found exact match for brace type: {frameProps.Name}");
                return _braceTypes[searchName];
            }

            // Try partial matches
            foreach (var kvp in _braceTypes)
            {
                if (kvp.Key.Contains(searchName) || searchName.Contains(kvp.Key))
                {
                    Debug.WriteLine($"Found partial match for brace type: {frameProps.Name} -> {kvp.Value.Name}");
                    return kvp.Value;
                }
            }

            // Return first available as fallback
            if (_braceTypes.Count > 0)
            {
                var fallbackSymbol = _braceTypes.Values.First();
                Debug.WriteLine($"No match found for brace type {frameProps.Name}, using fallback: {fallbackSymbol.Name}");
                return fallbackSymbol;
            }

            Debug.WriteLine($"No brace types available for: {frameProps.Name}");
            return null;
        }

        // Get frame properties by ID
        private FrameProperties GetFrameProperties(string framePropertiesId, BaseModel model)
        {
            if (string.IsNullOrEmpty(framePropertiesId) || model?.Properties?.FrameProperties == null)
                return null;

            return model.Properties.FrameProperties.FirstOrDefault(fp => fp.Id == framePropertiesId);
        }

        // Helper method to get floor thickness at a specific level
        private double GetFloorThicknessAtLevel(string levelId, BaseModel model)
        {
            try
            {
                // Default thickness in feet if no floor found
                double defaultThickness = 0.75; // 9 inches converted to feet

                if (model?.Elements?.Floors != null)
                {
                    var floorsAtLevel = model.Elements.Floors.Where(f => f.LevelId == levelId);
                    if (floorsAtLevel.Any())
                    {
                        // Get the floor properties to determine thickness
                        var firstFloor = floorsAtLevel.First();
                        if (!string.IsNullOrEmpty(firstFloor.FloorPropertiesId) &&
                            model.Properties?.FloorProperties != null)
                        {
                            var floorProps = model.Properties.FloorProperties.FirstOrDefault(
                                fp => fp.Id == firstFloor.FloorPropertiesId);
                            if (floorProps != null)
                            {
                                // Convert thickness from inches to feet
                                return floorProps.Thickness / 12.0;
                            }
                        }
                    }
                }

                return defaultThickness;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting floor thickness: {ex.Message}");
                return 0.75; // Return default thickness on error
            }
        }

        // Helper method to set parameter by name with error handling
        private void SetParameterByName(DB.FamilyInstance instance, string paramName, DB.ElementId value, string logPrefix)
        {
            try
            {
                DB.Parameter param = instance.LookupParameter(paramName);
                if (param != null && !param.IsReadOnly && param.StorageType == DB.StorageType.ElementId)
                {
                    param.Set(value);
                    Debug.WriteLine($"{logPrefix} set successfully via name '{paramName}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set parameter {paramName}: {ex.Message}");
            }
        }

        public int Import(List<CE.Brace> braces, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;

            foreach (var jsonBrace in braces)
            {
                try
                {
                    // Skip if required data is missing
                    if (string.IsNullOrEmpty(jsonBrace.BaseLevelId) ||
                        string.IsNullOrEmpty(jsonBrace.TopLevelId) ||
                        jsonBrace.StartPoint == null ||
                        jsonBrace.EndPoint == null)
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} due to missing data.");
                        continue;
                    }

                    // Get the base and top level ElementIds
                    if (!levelIdMap.TryGetValue(jsonBrace.BaseLevelId, out DB.ElementId baseLevelId) ||
                        !levelIdMap.TryGetValue(jsonBrace.TopLevelId, out DB.ElementId topLevelId))
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} due to missing level mapping.");
                        continue;
                    }

                    // Get base and top levels
                    DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                    DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;
                    if (baseLevel == null || topLevel == null)
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} due to invalid level.");
                        continue;
                    }

                    // Check that base and top levels have different elevations
                    if (Math.Abs(baseLevel.Elevation - topLevel.Elevation) < 0.01)
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} due to identical base and top level elevations.");
                        continue;
                    }

                    // Get frame properties and find appropriate brace type
                    var frameProps = GetFrameProperties(jsonBrace.FramePropertiesId, model);
                    DB.FamilySymbol familySymbol = FindBraceType(frameProps);

                    if (familySymbol == null)
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} because no suitable family symbol could be found.");
                        continue;
                    }

                    // Make sure the family symbol is active
                    if (!familySymbol.IsActive)
                    {
                        try { familySymbol.Activate(); }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error activating family symbol: {ex.Message}");
                            continue;
                        }
                    }

                    // Create curve for brace
                    // CONSISTENT CONVENTION: startPoint is BOTTOM, endPoint is TOP

                    // Get the elevations of the two levels
                    double baseElevation = baseLevel.ProjectElevation;
                    double topElevation = topLevel.ProjectElevation;

                    // Calculate floor thickness at the top level and adjust top elevation
                    double floorThickness = GetFloorThicknessAtLevel(jsonBrace.TopLevelId, model);
                    double topOffset = -floorThickness; // Negative to position below the floor
                    double adjustedTopElevation = topElevation + topOffset;

                    Debug.WriteLine($"Brace {jsonBrace.Id}: Base elevation: {baseElevation:F3}, Top elevation: {topElevation:F3}, Floor thickness: {floorThickness:F3}, Adjusted top elevation: {adjustedTopElevation:F3}");

                    // Convert the 2D points to 3D points with correct Z coordinates
                    DB.XYZ basePoint = Helpers.ConvertToRevitCoordinates(jsonBrace.StartPoint, baseElevation);
                    DB.XYZ topPoint = Helpers.ConvertToRevitCoordinates(jsonBrace.EndPoint, adjustedTopElevation);

                    // Create the line from bottom to top with adjusted top elevation
                    DB.Line braceLine = DB.Line.CreateBound(basePoint, topPoint);

                    // Ensure the points are not too close
                    if (topPoint.DistanceTo(basePoint) < 0.1)
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} due to start and end points being too close.");
                        continue;
                    }

                    // Create the structural brace
                    DB.FamilyInstance brace = _doc.Create.NewFamilyInstance(
                        braceLine,
                        familySymbol,
                        baseLevel,
                        DB.Structure.StructuralType.Brace);

                    try
                    {
                        // Set reference level to base level
                        DB.Parameter refLevelParam = brace.get_Parameter(DB.BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                        if (refLevelParam != null && !refLevelParam.IsReadOnly)
                        {
                            refLevelParam.Set(baseLevelId);
                            Debug.WriteLine($"Brace {jsonBrace.Id}: Set reference level to base level");
                        }

                        // Set top level parameter
                        DB.Parameter topLevelParam = brace.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                        if (topLevelParam != null && !topLevelParam.IsReadOnly)
                        {
                            topLevelParam.Set(topLevelId);
                            Debug.WriteLine($"Brace {jsonBrace.Id}: Top level set successfully");
                        }
                    }
                    catch (Exception paramEx)
                    {
                        Debug.WriteLine($"Error setting brace parameters: {paramEx.Message}");
                        // Continue with brace creation even if parameter setting fails
                    }

                    // Set attachment level references in a separate try block
                    try
                    {
                        // Try multiple parameter names that might be used for attachment levels
                        SetParameterByName(brace, "Start Level Reference", topLevelId, $"Brace {jsonBrace.Id}: Start level reference");
                        SetParameterByName(brace, "End Level Reference", baseLevelId, $"Brace {jsonBrace.Id}: End level reference");

                        // Try built-in parameters
                        DB.Parameter startLevelParam = brace.get_Parameter(DB.BuiltInParameter.STRUCTURAL_ATTACHMENT_START_LEVEL_REFERENCE);
                        if (startLevelParam != null && !startLevelParam.IsReadOnly)
                        {
                            startLevelParam.Set(topLevelId);
                            Debug.WriteLine($"Brace {jsonBrace.Id}: Start level attachment set to top level");
                        }

                        DB.Parameter endLevelParam = brace.get_Parameter(DB.BuiltInParameter.STRUCTURAL_ATTACHMENT_END_LEVEL_REFERENCE);
                        if (endLevelParam != null && !endLevelParam.IsReadOnly)
                        {
                            endLevelParam.Set(baseLevelId);
                            Debug.WriteLine($"Brace {jsonBrace.Id}: End level attachment set to base level");
                        }
                    }
                    catch (Exception attachEx)
                    {
                        Debug.WriteLine($"Error setting brace attachment level parameters: {attachEx.Message}");
                        // Continue with brace creation even if parameter setting fails
                    }

                    count++;
                    Debug.WriteLine($"Created brace {jsonBrace.Id} with type {familySymbol.Name}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating brace {jsonBrace.Id}: {ex.Message}");
                }
            }

            return count;
        }
    }
}
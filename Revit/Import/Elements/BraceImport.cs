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

                    // Also add by family name + symbol name for more specific matching
                    string combinedKey = $"{symbol.Family.Name}_{symbol.Name}".ToUpper();
                    if (!_braceTypes.ContainsKey(combinedKey))
                    {
                        _braceTypes[combinedKey] = symbol;
                    }
                }

                Debug.WriteLine($"No dedicated brace types found, using {_braceTypes.Count} generic structural framing types as fallback");
            }
        }

        // Find appropriate brace type based on frame properties
        private DB.FamilySymbol FindBraceType(Core.Models.Properties.FrameProperties frameProps)
        {
            // Try to find an HSS type to use as default
            DB.FamilySymbol hssType = _braceTypes
                .Where(kvp => kvp.Key.Contains("HSS"))
                .Select(kvp => kvp.Value)
                .FirstOrDefault();

            // If no HSS type found, use the first type available
            DB.FamilySymbol defaultType = hssType ?? _braceTypes.Values.FirstOrDefault();

            if (frameProps == null)
            {
                return defaultType;
            }

            // Try to match by name
            if (!string.IsNullOrEmpty(frameProps.Name))
            {
                string typeName = frameProps.Name.ToUpper();
                if (_braceTypes.TryGetValue(typeName, out DB.FamilySymbol typeByName))
                {
                    return typeByName;
                }
            }

            // Try to match by section name for steel sections
            if (frameProps.SteelProps != null && !string.IsNullOrEmpty(frameProps.SteelProps.SectionName))
            {
                string sectionName = frameProps.SteelProps.SectionName.ToUpper();
                if (_braceTypes.TryGetValue(sectionName, out DB.FamilySymbol typeBySectionName))
                {
                    return typeBySectionName;
                }

                // If exact match not found, try to match by section type
                var sectionType = frameProps.SteelProps.SectionType;

                // Get braces matching this section type prefix
                var matches = _braceTypes.Where(kvp =>
                    kvp.Key.StartsWith(sectionType.ToString()) ||
                    kvp.Key.Contains(sectionType.ToString()))
                    .ToList();

                if (matches.Any())
                {
                    return matches.First().Value;
                }
            }

            // Try to match by concrete section if it's a concrete frame
            if (frameProps.ConcreteProps != null)
            {
                // Try to find a concrete bracing element
                var concreteTypes = _braceTypes.Where(kvp =>
                    kvp.Key.Contains("CONCRETE") ||
                    kvp.Key.Contains("CONC"))
                    .ToList();

                if (concreteTypes.Any())
                {
                    return concreteTypes.First().Value;
                }
            }

            // If no match found by name or shape, prioritize HSS type
            return defaultType;
        }

        // Get frame properties for a brace
        private Core.Models.Properties.FrameProperties GetFrameProperties(
            string framePropertiesId, BaseModel model)
        {
            if (string.IsNullOrEmpty(framePropertiesId) || model?.Properties?.FrameProperties == null)
            {
                return null;
            }

            return model.Properties.FrameProperties.FirstOrDefault(fp =>
                fp.Id == framePropertiesId);
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
                    double startPointZ = baseLevel.ProjectElevation;
                    double endPointZ = topLevel.ProjectElevation;
                    DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonBrace.StartPoint, startPointZ);
                    DB.XYZ endPoint = Helpers.ConvertToRevitCoordinates(jsonBrace.EndPoint, endPointZ);

                    // Ensure the points are not too close
                    if (startPoint.DistanceTo(endPoint) < 0.1)
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} due to start and end points being too close.");
                        continue;
                    }

                    DB.Line braceLine = DB.Line.CreateBound(startPoint, endPoint);

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

                        // Calculate top offset based on floor thickness at the top level
                        double floorThickness = GetFloorThicknessAtLevel(jsonBrace.TopLevelId, model);
                        double topOffset = -floorThickness; // Negative to position below the floor

                        // Set top offset
                        DB.Parameter topOffsetParam = brace.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                        if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                        {
                            topOffsetParam.Set(topOffset);
                            Debug.WriteLine($"Brace {jsonBrace.Id}: Top offset of {topOffset} feet applied");
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
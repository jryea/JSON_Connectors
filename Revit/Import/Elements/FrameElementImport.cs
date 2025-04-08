using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Revit.Utilities;
using Core.Models;

namespace Revit.Import.Elements
{
    // Combined class for importing frame elements (beams and braces) from JSON into Revit
    public class FrameElementImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FamilySymbol> _beamTypes;
        private Dictionary<string, DB.FamilySymbol> _braceTypes;

        public FrameElementImport(DB.Document doc)
        {
            _doc = doc;
            InitializeFrameElementTypes();
        }

        // Initialize dictionaries of available beam and brace family types
        private void InitializeFrameElementTypes()
        {
            _beamTypes = new Dictionary<string, DB.FamilySymbol>();
            _braceTypes = new Dictionary<string, DB.FamilySymbol>();

            // Get all structural framing family symbols
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.FamilySymbol));
            collector.OfCategory(DB.BuiltInCategory.OST_StructuralFraming);

            foreach (DB.FamilySymbol symbol in collector)
            {
                if (!symbol.IsActive)
                {
                    symbol.Activate();
                }

                string key = symbol.Name.ToUpper();

                // Determine if this is a beam or brace based on family name
                bool isBrace = symbol.Family.Name.ToUpper().Contains("BRACE") ||
                               key.Contains("BRACE");

                if (isBrace)
                {
                    if (!_braceTypes.ContainsKey(key))
                    {
                        _braceTypes[key] = symbol;
                    }
                }
                else
                {
                    if (!_beamTypes.ContainsKey(key))
                    {
                        _beamTypes[key] = symbol;
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
                else
                {
                    if (!_beamTypes.ContainsKey(combinedKey))
                    {
                        _beamTypes[combinedKey] = symbol;
                    }
                }
            }

            Debug.WriteLine($"Loaded {_beamTypes.Count} beam family types and {_braceTypes.Count} brace family types");

            // If no brace types were found specifically, use beam types as fallback
            if (_braceTypes.Count == 0)
            {
                _braceTypes = _beamTypes;
            }
        }

        // Find appropriate frame element type based on frame properties
        private DB.FamilySymbol FindFrameElementType(
            Core.Models.Properties.FrameProperties frameProps,
            bool isBrace)
        {
            var typeDict = isBrace ? _braceTypes : _beamTypes;

            // Default to the first type if we can't find a match
            DB.FamilySymbol defaultType = typeDict.Values.FirstOrDefault();

            if (frameProps == null)
            {
                return defaultType;
            }

            // Try to match by name
            if (!string.IsNullOrEmpty(frameProps.Name))
            {
                string typeName = frameProps.Name.ToUpper();
                if (typeDict.TryGetValue(typeName, out DB.FamilySymbol typeByName))
                {
                    return typeByName;
                }
            }

            // Try to match by shape
            if (!string.IsNullOrEmpty(frameProps.Shape))
            {
                // Look for elements that contain the shape designation
                var matches = typeDict.Where(kvp =>
                    kvp.Key.Contains(frameProps.Shape.ToUpper())).ToList();

                if (matches.Any())
                {
                    return matches.First().Value;
                }
            }

            return defaultType;
        }

        // Get frame properties for an element - MODIFIED
        private Core.Models.Properties.FrameProperties GetFrameProperties(
            string framePropertiesId, BaseModel model)
        {
            // If no framePropertiesId specified, use the first available frame property
            if (string.IsNullOrEmpty(framePropertiesId) || model?.Properties?.FrameProperties == null ||
                !model.Properties.FrameProperties.Any())
            {
                return model?.Properties?.FrameProperties?.FirstOrDefault();
            }

            var result = model.Properties.FrameProperties.FirstOrDefault(fp =>
                fp.Id == framePropertiesId);

            // If specific property not found, return first available
            if (result == null)
            {
                return model.Properties.FrameProperties.FirstOrDefault();
            }

            return result;
        }

        // Imports beams from the JSON model into Revit
        public int ImportBeams(List<CE.Beam> beams, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;

            foreach (var jsonBeam in beams)
            {
                try
                {
                    // Skip if required data is missing
                    if (string.IsNullOrEmpty(jsonBeam.LevelId) ||
                        jsonBeam.StartPoint == null ||
                        jsonBeam.EndPoint == null)
                    {
                        Debug.WriteLine($"Skipping beam {jsonBeam.Id} due to missing data.");
                        continue;
                    }

                    // Get the ElementId for the level
                    if (!levelIdMap.TryGetValue(jsonBeam.LevelId, out DB.ElementId levelId))
                    {
                        Debug.WriteLine($"Skipping beam {jsonBeam.Id} due to missing level mapping.");
                        continue;
                    }

                    // Get Level
                    DB.Level level = _doc.GetElement(levelId) as DB.Level;
                    if (level == null)
                    {
                        Debug.WriteLine($"Skipping beam {jsonBeam.Id} due to invalid level.");
                        continue;
                    }

                    // Get frame properties and find appropriate beam type - MODIFIED
                    var frameProps = GetFrameProperties(jsonBeam.FramePropertiesId, model);
                    DB.FamilySymbol familySymbol = FindFrameElementType(frameProps, false);

                    if (familySymbol == null)
                    {
                        Debug.WriteLine($"Skipping beam {jsonBeam.Id} because no suitable family symbol could be found.");
                        continue;
                    }

                    // Make sure the family symbol is active
                    if (!familySymbol.IsActive)
                    {
                        familySymbol.Activate();
                    }

                    // Create curve for beam
                    DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonBeam.StartPoint);
                    DB.XYZ endPoint = Helpers.ConvertToRevitCoordinates(jsonBeam.EndPoint);
                    DB.Line beamLine = DB.Line.CreateBound(startPoint, endPoint);

                    // Create the structural beam
                    DB.FamilyInstance beam = _doc.Create.NewFamilyInstance(
                        beamLine,
                        familySymbol,
                        level,
                        DB.Structure.StructuralType.Beam);

                    // Set beam reference level
                    beam.get_Parameter(DB.BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).Set(levelId);

                    // Get the floor thickness for the level - default to 0 if not found
                    double floorThickness = 0;

                    // Find a floor on this level that has properties
                    var levelFloors = model.Elements.Floors.Where(f => f.LevelId == jsonBeam.LevelId).ToList();
                    if (levelFloors.Any() && !string.IsNullOrEmpty(levelFloors[0].FloorPropertiesId))
                    {
                        // Get floor properties
                        var floorProps = model.Properties.FloorProperties
                            .FirstOrDefault(fp => fp.Id == levelFloors[0].FloorPropertiesId);

                        if (floorProps != null)
                        {
                            // Get thickness in feet (convert from model units)
                            floorThickness = floorProps.Thickness / 12.0;
                            Debug.WriteLine($"Found floor thickness of {floorThickness} feet for level {level.Name}");
                        }
                    }

                    // Set negative offset equal to floor thickness
                    double startOffset = -floorThickness;
                    double endOffset = -floorThickness;

                    beam.get_Parameter(DB.BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION).Set(startOffset);
                    beam.get_Parameter(DB.BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION).Set(endOffset);

                    Debug.WriteLine($"Set beam offsets to {startOffset} feet based on floor thickness");

                    count++;
                    Debug.WriteLine($"Created beam {jsonBeam.Id} with type {familySymbol.Name}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating beam {jsonBeam.Id}: {ex.Message}");
                }
            }

            return count;
        }

        // Imports braces from the JSON model into Revit
        public int ImportBraces(List<CE.Brace> braces, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;

            foreach (var jsonBrace in braces)
            {
                try
                {
                    // Skip if required data is missing
                    if (string.IsNullOrEmpty(jsonBrace.BaseLevelId) ||
                        jsonBrace.StartPoint == null ||
                        jsonBrace.EndPoint == null)
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} due to missing data.");
                        continue;
                    }

                    // Get the base level ElementId
                    if (!levelIdMap.TryGetValue(jsonBrace.BaseLevelId, out DB.ElementId baseLevelId))
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} due to missing level mapping.");
                        continue;
                    }

                    // Get base level
                    DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                    if (baseLevel == null)
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} due to invalid level.");
                        continue;
                    }

                    // Get frame properties and find appropriate brace type - MODIFIED
                    var frameProps = GetFrameProperties(jsonBrace.FramePropertiesId, model);
                    DB.FamilySymbol familySymbol = FindFrameElementType(frameProps, true);

                    if (familySymbol == null)
                    {
                        Debug.WriteLine($"Skipping brace {jsonBrace.Id} because no suitable family symbol could be found.");
                        continue;
                    }

                    // Make sure the family symbol is active
                    if (!familySymbol.IsActive)
                    {
                        familySymbol.Activate();
                    }

                    // Create curve for brace
                    DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonBrace.StartPoint);
                    DB.XYZ endPoint = Helpers.ConvertToRevitCoordinates(jsonBrace.EndPoint);
                    DB.Line braceLine = DB.Line.CreateBound(startPoint, endPoint);

                    // Create the structural brace
                    DB.FamilyInstance brace = _doc.Create.NewFamilyInstance(
                        braceLine,
                        familySymbol,
                        baseLevel,
                        DB.Structure.StructuralType.Brace);

                    // Set top level if available
                    if (!string.IsNullOrEmpty(jsonBrace.TopLevelId) &&
                        levelIdMap.TryGetValue(jsonBrace.TopLevelId, out DB.ElementId topLevelId))
                    {
                        DB.Parameter topLevelParam = brace.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                        if (topLevelParam != null && !topLevelParam.IsReadOnly)
                        {
                            topLevelParam.Set(topLevelId);
                        }
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
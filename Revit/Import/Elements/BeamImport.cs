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
    // Imports beam elements from JSON into Revit
    public class BeamImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FamilySymbol> _beamTypes;

        public BeamImport(DB.Document doc)
        {
            _doc = doc;
            InitializeBeamTypes();
        }

        // Initialize dictionary of available beam family types
        private void InitializeBeamTypes()
        {
            _beamTypes = new Dictionary<string, DB.FamilySymbol>();

            // Get all structural framing family symbols that could be beams
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.FamilySymbol));
            collector.OfCategory(DB.BuiltInCategory.OST_StructuralFraming);

            foreach (DB.FamilySymbol symbol in collector)
            {
                string key = symbol.Name.ToUpper();
                
                if (!_beamTypes.ContainsKey(key))
                {
                    _beamTypes[key] = symbol;
                }

                // Also add by family name + symbol name for more specific matching
                string combinedKey = $"{symbol.Family.Name}_{symbol.Name}".ToUpper();
               
                if (!_beamTypes.ContainsKey(combinedKey))
                {
                    _beamTypes[combinedKey] = symbol;
                }
                
            }

            Debug.WriteLine($"Loaded {_beamTypes.Count} beam family types");
        }

        // Find appropriate beam type based on frame properties
        

        // Get frame properties for an element
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

        private DB.FamilySymbol FindBeamType(Core.Models.Properties.FrameProperties frameProps)
        {
            // Default to the first type if we can't find a match
            DB.FamilySymbol defaultType = _beamTypes.Values.FirstOrDefault();

            if (frameProps == null)
            {
                return defaultType;
            }

            // Try to match by name
            if (!string.IsNullOrEmpty(frameProps.Name))
            {
                string typeName = frameProps.Name.ToUpper();
                if (_beamTypes.TryGetValue(typeName, out DB.FamilySymbol typeByName))
                {
                    return typeByName;
                }
            }

            // Enhanced section type matching based on SteelProps.SectionType
            if (frameProps.Type == FrameMaterialType.Steel && frameProps.SteelProps != null)
            {
                var sectionType = frameProps.SteelProps.SectionType;

                // Attempt to match family by section type
                switch (sectionType)
                {
                    case SteelSectionType.W:
                        // Find Wide Flange sections
                        var wSections = _beamTypes.Where(kvp =>
                            kvp.Key.StartsWith("W") ||
                            kvp.Key.Contains("WIDE") ||
                            kvp.Key.Contains("FLANGE"))
                            .ToList();

                        if (wSections.Any())
                            return wSections.First().Value;
                        break;

                    case SteelSectionType.HSS:
                        // Find HSS sections
                        var hssSections = _beamTypes.Where(kvp =>
                            kvp.Key.Contains("HSS") ||
                            kvp.Key.Contains("TUBE"))
                            .ToList();

                        if (hssSections.Any())
                            return hssSections.First().Value;
                        break;

                    case SteelSectionType.PIPE:
                        // Find Pipe sections
                        var pipeSections = _beamTypes.Where(kvp =>
                            kvp.Key.Contains("PIPE"))
                            .ToList();

                        if (pipeSections.Any())
                            return pipeSections.First().Value;
                        break;

                    case SteelSectionType.C:
                        // Find Channel sections
                        var cSections = _beamTypes.Where(kvp =>
                            kvp.Key.StartsWith("C") ||
                            kvp.Key.Contains("CHANNEL"))
                            .ToList();

                        if (cSections.Any())
                            return cSections.First().Value;
                        break;

                    case SteelSectionType.L:
                        // Find Angle sections
                        var lSections = _beamTypes.Where(kvp =>
                            kvp.Key.StartsWith("L") ||
                            kvp.Key.Contains("ANGLE"))
                            .ToList();

                        if (lSections.Any())
                            return lSections.First().Value;
                        break;

                    default:
                        // For other section types, try to find family by section type name
                        var typeSections = _beamTypes.Where(kvp =>
                            kvp.Key.Contains(sectionType.ToString()))
                            .ToList();

                        if (typeSections.Any())
                            return typeSections.First().Value;
                        break;
                }
            }
            else if (frameProps.Type == FrameMaterialType.Concrete && frameProps.ConcreteProps != null)
            {
                // For concrete beams, try to find a concrete beam type
                var concreteBeams = _beamTypes.Where(kvp =>
                    kvp.Key.Contains("CONCRETE") ||
                    kvp.Key.Contains("CONC"))
                    .ToList();

                if (concreteBeams.Any())
                {
                    return concreteBeams.First().Value;
                }
            }

            return defaultType;
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

        public int Import(List<CE.Beam> beams, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;

            foreach (var jsonBeam in beams)
            {
                try
                {
                    // Skip if required data is missing - but don't check framePropertiesId
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

                    // Get frame properties and find appropriate beam type
                    var frameProps = GetFrameProperties(jsonBeam.FramePropertiesId, model);
                    DB.FamilySymbol familySymbol = FindBeamType(frameProps);

                    if (familySymbol == null)
                    {
                        Debug.WriteLine($"Skipping beam {jsonBeam.Id} because no suitable family symbol could be found.");
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

                    // Calculate offset based on floor thickness at this level
                    double floorThickness = GetFloorThicknessAtLevel(jsonBeam.LevelId, model);
                    double offset = -floorThickness; // Negative to position below the floor

                    Debug.WriteLine($"Beam {jsonBeam.Id}: Offset of {offset} feet applied based on floor thickness of {floorThickness} feet at level {level.Name}");

                    // Set start and end level offsets safely
                    try
                    {
                        beam.get_Parameter(DB.BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION).Set(offset);
                        beam.get_Parameter(DB.BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION).Set(offset);
                    }
                    catch (Exception paramEx)
                    {
                        Debug.WriteLine($"Error setting beam offset parameters: {paramEx.Message}");
                        // Continue with beam creation even if offset setting fails
                    }

                    

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
    }
}
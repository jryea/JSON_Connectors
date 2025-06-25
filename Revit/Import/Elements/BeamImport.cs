using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
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
        private Dictionary<string, DB.FamilySymbol> _joistGirderTypes;
        private Dictionary<string, DB.FamilySymbol> _barJoistTypes;

        public BeamImport(DB.Document doc)
        {
            _doc = doc;
            InitializeBeamTypes();
            InitializeJoistTypes();
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

        // Initialize dictionaries of available joist girder and bar joist family types
        private void InitializeJoistTypes()
        {
            _joistGirderTypes = new Dictionary<string, DB.FamilySymbol>();
            _barJoistTypes = new Dictionary<string, DB.FamilySymbol>();

            // Get all structural framing family symbols
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.FamilySymbol));
            collector.OfCategory(DB.BuiltInCategory.OST_StructuralFraming);

            foreach (DB.FamilySymbol symbol in collector)
            {
                string symbolName = symbol.Name.ToUpper();
                string familyName = symbol.Family.Name.ToUpper();

                // Identify joist girders (sections starting with {number}G)
                if (IsJoistGirder(symbolName))
                {
                    if (!_joistGirderTypes.ContainsKey(symbolName))
                    {
                        _joistGirderTypes[symbolName] = symbol;
                    }
                }

                // Identify bar joists (containing K or LH with steel material)
                if (IsBarJoist(symbol, symbolName, familyName))
                {
                    if (!_barJoistTypes.ContainsKey(symbolName))
                    {
                        _barJoistTypes[symbolName] = symbol;
                    }
                }
            }

            Debug.WriteLine($"Loaded {_joistGirderTypes.Count} joist girder types");
            Debug.WriteLine($"Loaded {_barJoistTypes.Count} bar joist types");
        }

        // Check if section name represents a joist girder
        private bool IsJoistGirder(string sectionName)
        {
            // Match pattern like "40G5N15K" - starts with number followed by G
            Regex joistGirderPattern = new Regex(@"^\d+G", RegexOptions.IgnoreCase);
            return joistGirderPattern.IsMatch(sectionName);
        }

        // Check if family symbol represents a bar joist
        private bool IsBarJoist(DB.FamilySymbol symbol, string symbolName, string familyName)
        {
            // Check for K or LH in name and steel material
            bool hasJoistDesignation = symbolName.Contains("K") || symbolName.Contains("LH") ||
                                       familyName.Contains("JOIST");

            if (!hasJoistDesignation)
                return false;

            // Verify it's steel material (Revit-specific material detection)
            return IsRevitSteelMaterial(symbol);
        }

        // Helper to detect steel material in Revit context (not RAM)
        private bool IsRevitSteelMaterial(DB.FamilySymbol symbol)
        {
            // Try to get material from structural material parameter
            DB.Parameter structMatParam = symbol.get_Parameter(DB.BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (structMatParam != null && structMatParam.HasValue &&
                structMatParam.StorageType == DB.StorageType.ElementId)
            {
                DB.ElementId materialId = structMatParam.AsElementId();
                if (materialId != DB.ElementId.InvalidElementId)
                {
                    DB.Material material = _doc.GetElement(materialId) as DB.Material;
                    if (material != null)
                    {
                        string materialName = material.Name.ToUpper();
                        return materialName.Contains("STEEL") || materialName.Contains("A992") || materialName.Contains("A36");
                    }
                }
            }

            // Fallback to name-based detection
            string symbolName = symbol.Name.ToUpper();
            string familyName = symbol.Family.Name.ToUpper();
            return symbolName.Contains("STEEL") || familyName.Contains("STEEL") ||
                   symbolName.StartsWith("W") || symbolName.Contains("HSS");
        }

        // Find appropriate beam type based on frame properties with joist girder support
        private DB.FamilySymbol FindBeamType(Core.Models.Properties.FrameProperties frameProps)
        {
            // Check for joist girders first
            if (frameProps?.SteelProps?.SectionName != null &&
                IsJoistGirder(frameProps.SteelProps.SectionName))
            {
                return FindJoistGirderType(frameProps.SteelProps.SectionName);
            }

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

                    case SteelSectionType.JOIST_GIRDER:
                        // Handle joist girder sections - this will trigger the joist girder logic
                        return FindJoistGirderType(frameProps.SteelProps.SectionName);

                    case SteelSectionType.BAR_JOIST:
                        // Find bar joist sections
                        var barJoist = _barJoistTypes.Values.FirstOrDefault();
                        if (barJoist != null)
                            return barJoist;
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
                    return concreteBeams.First().Value;
            }

            return defaultType;
        }

        // Main joist girder finding logic with cascading fallbacks
        private DB.FamilySymbol FindJoistGirderType(string sectionName)
        {
            string sectionNameUpper = sectionName.ToUpper();

            // 1. Look for exact match
            if (_joistGirderTypes.TryGetValue(sectionNameUpper, out DB.FamilySymbol exactMatch))
            {
                Debug.WriteLine($"Found exact joist girder match: {exactMatch.Name}");
                return exactMatch;
            }

            // 2. Look for partial match with same number prefix
            string numberPrefix = ExtractNumberPrefix(sectionNameUpper);
            if (!string.IsNullOrEmpty(numberPrefix))
            {
                var partialMatch = FindPartialJoistGirderMatch(numberPrefix);
                if (partialMatch != null)
                {
                    // Duplicate and rename if not exact match
                    var duplicatedType = DuplicateJoistGirderType(partialMatch, sectionName);
                    Debug.WriteLine($"Found partial match, duplicated: {partialMatch.Name} -> {sectionName}");
                    return duplicatedType;
                }
            }

            // 3. Default to any joist girder with same number prefix
            if (!string.IsNullOrEmpty(numberPrefix))
            {
                var anyWithPrefix = _joistGirderTypes.Values.FirstOrDefault(s =>
                    s.Name.ToUpper().StartsWith(numberPrefix + "G"));
                if (anyWithPrefix != null)
                {
                    Debug.WriteLine($"Found any joist girder with prefix {numberPrefix}: {anyWithPrefix.Name}");
                    return anyWithPrefix;
                }
            }

            // 4. Default to any bar joist (K or LH with steel material)
            var anyBarJoist = _barJoistTypes.Values.FirstOrDefault();
            if (anyBarJoist != null)
            {
                Debug.WriteLine($"Defaulting to bar joist: {anyBarJoist.Name}");
                return anyBarJoist;
            }

            // 5. Default to any steel beam
            var steelBeam = FindSteelBeamFallback();
            if (steelBeam != null)
            {
                Debug.WriteLine($"Defaulting to steel beam: {steelBeam.Name}");
                return steelBeam;
            }

            // 6. Final fallback to any beam
            var anyBeam = _beamTypes.Values.FirstOrDefault();
            Debug.WriteLine($"Final fallback to any beam: {anyBeam?.Name ?? "NULL"}");
            return anyBeam;
        }

        // Extract number prefix from section name (e.g., "40G5N15K" -> "40")
        private string ExtractNumberPrefix(string sectionName)
        {
            Regex numberPattern = new Regex(@"^(\d+)G", RegexOptions.IgnoreCase);
            Match match = numberPattern.Match(sectionName);
            return match.Success ? match.Groups[1].Value : null;
        }

        // Find partial match for joist girder with same number prefix
        private DB.FamilySymbol FindPartialJoistGirderMatch(string numberPrefix)
        {
            string searchPattern = numberPrefix + "G";
            return _joistGirderTypes.Values.FirstOrDefault(s =>
                s.Name.ToUpper().StartsWith(searchPattern));
        }

        // Duplicate joist girder type and rename
        private DB.FamilySymbol DuplicateJoistGirderType(DB.FamilySymbol baseType, string newName)
        {
            try
            {
                // Check if type with this name already exists
                if (_joistGirderTypes.TryGetValue(newName.ToUpper(), out DB.FamilySymbol existing))
                {
                    Debug.WriteLine($"Joist girder type {newName} already exists, using existing");
                    return existing;
                }

                Debug.WriteLine($"Duplicating joist girder type {baseType.Name} as {newName}");
                var newType = baseType.Duplicate(newName) as DB.FamilySymbol;

                if (newType != null)
                {
                    // Activate the new type
                    if (!newType.IsActive)
                    {
                        newType.Activate();
                    }

                    // Add to cache
                    _joistGirderTypes[newName.ToUpper()] = newType;
                    Debug.WriteLine($"Successfully created joist girder type: {newType.Name}");
                    return newType;
                }
                else
                {
                    Debug.WriteLine("Failed to duplicate joist girder type");
                    return baseType;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error duplicating joist girder type: {ex.Message}");
                return baseType;
            }
        }

        // Find steel beam as fallback
        private DB.FamilySymbol FindSteelBeamFallback()
        {
            // Look for common steel beam types
            var steelBeams = _beamTypes.Where(kvp =>
                kvp.Key.StartsWith("W") ||
                kvp.Key.Contains("STEEL") ||
                kvp.Key.Contains("BEAM"))
                .ToList();

            return steelBeams.FirstOrDefault().Value;
        }

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

        // Import beam elements from JSON model
        public int Import(List<CE.Beam> beams, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            if (beams == null || beams.Count == 0)
            {
                Debug.WriteLine("No beams to import.");
                return 0;
            }

            int count = 0;

            foreach (var jsonBeam in beams)
            {
                try
                {
                    // Get the levelId for beam creation using the provided mapping
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

        // Helper method to get floor thickness at a specific level
        private double GetFloorThicknessAtLevel(string levelId, BaseModel model)
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
    }
}
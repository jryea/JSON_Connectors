using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;
using Core.Models;
using System.Diagnostics;

namespace Revit.Import.Properties
{
    public class FloorPropertiesImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FloorType> _floorTypeCache;
        private DB.FloorType _defaultConcreteType;
        private DB.FloorType _defaultDeckType;
        private DB.FloorType _defaultFloorType;
        private List<DB.FloorType> _allFloorTypes;

        private double _thicknessInInches;
        private double _thicknessInFeet;
        private double _deckThicknessInInches;
        private double _deckThicknessInFeet;
        private double _concreteThicknessInInches;
        private double _concreteThicknessInFeet;

        public FloorPropertiesImport(DB.Document doc)
        {
            _doc = doc;
            _floorTypeCache = new Dictionary<string, DB.FloorType>();
            InitializeFloorTypes();
        }

        private void InitializeFloorTypes()
        {
            // Get all floor types from the document
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            _allFloorTypes = collector.OfClass(typeof(DB.FloorType))
                .Cast<DB.FloorType>()
                .ToList();

            if (_allFloorTypes.Count == 0)
            {
                Debug.WriteLine("WARNING: No floor types found in the document");
                return;
            }

            Debug.WriteLine($"Found {_allFloorTypes.Count} floor types in document:");
            foreach (var ft in _allFloorTypes)
            {
                Debug.WriteLine($"  - {ft.Name}");
            }

            // Find concrete types (broader search)
            var concreteTypes = _allFloorTypes
                .Where(ft => ft.Name.ToLowerInvariant().Contains("concrete"))
                .ToList();

            // Find deck types
            var deckTypes = _allFloorTypes
                .Where(ft => ft.Name.ToLowerInvariant().Contains("deck") ||
                           ft.Name.ToLowerInvariant().Contains("metal"))
                .ToList();

            // Set default concrete type (prefer specific thicknesses, then any concrete)
            _defaultConcreteType = concreteTypes.FirstOrDefault(ft => ft.Name.Contains("6\"") || ft.Name.Contains("6")) ??
                                 concreteTypes.FirstOrDefault(ft => ft.Name.Contains("8\"") || ft.Name.Contains("8")) ??
                                 concreteTypes.FirstOrDefault();

            // Set default deck type
            _defaultDeckType = deckTypes.FirstOrDefault(ft =>
                ft.Name.ToLowerInvariant().Contains("concrete") &&
                ft.Name.ToLowerInvariant().Contains("deck")) ??
                deckTypes.FirstOrDefault();

            // Set ultimate fallback (prefer concrete, then deck, then anything)
            _defaultFloorType = _defaultConcreteType ?? _defaultDeckType ?? _allFloorTypes.FirstOrDefault();

            Debug.WriteLine($"Default concrete type: {(_defaultConcreteType?.Name ?? "None")}");
            Debug.WriteLine($"Default deck type: {(_defaultDeckType?.Name ?? "None")}");
            Debug.WriteLine($"Default floor type: {(_defaultFloorType?.Name ?? "None")}");
        }

        public DB.FloorType FindOrCreateFloorType(FloorProperties floorProps, BaseModel model)
        {
            if (floorProps == null)
            {
                Debug.WriteLine("No floor properties provided, using default concrete type");
                return _defaultConcreteType ?? _defaultFloorType;
            }

            // Convert thickness once at class level
            _thicknessInInches = floorProps.Thickness;
            _thicknessInFeet = _thicknessInInches / 12.0;
            _deckThicknessInInches = floorProps.DeckProperties?.RibDepth ?? 3.0;
            _deckThicknessInFeet = _deckThicknessInInches / 12.0;
            _concreteThicknessInInches = _thicknessInInches - _deckThicknessInInches;
            _concreteThicknessInFeet = _concreteThicknessInInches / 12.0;

            string cacheKey = $"{floorProps.Type}_{floorProps.Thickness}_{floorProps.DeckProperties?.DeckType ?? "none"}";

            if (_floorTypeCache.ContainsKey(cacheKey))
            {
                return _floorTypeCache[cacheKey];
            }

            DB.FloorType selectedType = null;

            Debug.WriteLine($"Finding floor type for: {floorProps.Type}, Thickness: {_thicknessInInches}\" ({_thicknessInFeet:F3}ft)");

            switch (floorProps.Type)
            {
                case StructuralFloorType.FilledDeck:
                    selectedType = FindOrCreateFilledDeckType();
                    break;
                case StructuralFloorType.UnfilledDeck:
                    selectedType = FindOrCreateUnfilledDeckType();
                    break;
                case StructuralFloorType.Slab:
                    selectedType = FindOrCreateSlabType();
                    break;
                default:
                    Debug.WriteLine($"Unknown floor type: {floorProps.Type}, using slab fallback");
                    selectedType = FindOrCreateSlabType();
                    break;
            }

            // Final fallback
            if (selectedType == null)
            {
                selectedType = _defaultConcreteType ?? _defaultFloorType;
                Debug.WriteLine($"Using final fallback: {selectedType?.Name ?? "None"}");
            }

            _floorTypeCache[cacheKey] = selectedType;
            return selectedType;
        }

        private DB.FloorType FindOrCreateFilledDeckType()
        {
            string concreteThicknessStr = FormatThickness(_concreteThicknessInInches);
            string deckThicknessStr = FormatThickness(_deckThicknessInInches);

            // Try naming variations from project charter
            string[] nameVariations = {
                $"{concreteThicknessStr}\" Concrete on {deckThicknessStr}\" Metal Deck",
                $"{concreteThicknessStr}\" Concrete on {deckThicknessStr}\" Deck",
                $"{concreteThicknessStr} Concrete on {deckThicknessStr} Metal Deck",
                $"{concreteThicknessStr} Concrete on {deckThicknessStr} Deck"
            };

            foreach (var variation in nameVariations)
            {
                var existingType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.Trim().Equals(variation, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"Found existing deck type: '{existingType.Name}'");
                    return existingType;
                }
            }

            // Try to create new type
            if (_defaultDeckType != null)
            {
                string newTypeName = nameVariations[0];
                Debug.WriteLine($"Creating new deck type: '{newTypeName}'");
                return CreateFloorTypeWithThickness(_defaultDeckType, _thicknessInFeet, newTypeName);
            }

            // Enhanced fallback - find any floor type with StructuralDeck layer
            var deckFloorType = FindAnyFloorTypeWithStructuralDeck();
            if (deckFloorType != null)
            {
                string newTypeName = nameVariations[0];
                Debug.WriteLine($"Using StructuralDeck floor type as base: '{deckFloorType.Name}'");
                return CreateFilledDeckFloorType(deckFloorType, newTypeName);
            }

            Debug.WriteLine("No suitable deck type found, falling back to concrete slab");
            return FindOrCreateSlabType();
        }

        private DB.FloorType FindOrCreateUnfilledDeckType()
        {
            string deckThicknessStr = FormatThickness(_deckThicknessInInches);

            // Try naming variations for unfilled deck
            string[] nameVariations = {
                $"{deckThicknessStr}\" Metal Deck",
                $"{deckThicknessStr}\" Deck",
                $"{deckThicknessStr} Metal Deck",
                $"{deckThicknessStr} Deck"
            };

            foreach (var variation in nameVariations)
            {
                var existingType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.Trim().Equals(variation, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"Found existing unfilled deck type: '{existingType.Name}'");
                    return existingType;
                }
            }

            // Try to create new unfilled deck type with proper deck profile
            DB.FloorType newDeckType = CreateUnfilledDeckFloorType(nameVariations[0]);
            if (newDeckType != null)
            {
                return newDeckType;
            }

            Debug.WriteLine("No suitable unfilled deck type found, using default deck");
            return _defaultDeckType ?? _defaultFloorType;
        }

        private DB.FloorType FindOrCreateSlabType()
        {
            string thicknessStr = FormatThickness(_thicknessInInches);

            // Try naming variations for concrete slab
            string[] nameVariations = {
                $"{thicknessStr}\" Concrete",
                $"{thicknessStr} Concrete",
                $"Concrete {thicknessStr}\"",
                $"Concrete {thicknessStr}"
            };

            foreach (var variation in nameVariations)
            {
                var existingType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.Trim().Equals(variation, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"Found existing concrete type: '{existingType.Name}'");
                    return existingType;
                }
            }

            // Try to create new concrete type
            if (_defaultConcreteType != null)
            {
                string newTypeName = nameVariations[0];
                Debug.WriteLine($"Creating new concrete type: '{newTypeName}'");
                return CreateFloorTypeWithThickness(_defaultConcreteType, _thicknessInFeet, newTypeName);
            }

            Debug.WriteLine("No suitable concrete type found, using default");
            return _defaultConcreteType ?? _defaultFloorType;
        }

        private string FormatThickness(double thicknessInInches)
        {
            // Try to format as fraction
            if (thicknessInInches == Math.Floor(thicknessInInches))
            {
                return ((int)thicknessInInches).ToString();
            }

            // Handle common fractions
            double fractionalPart = thicknessInInches - Math.Floor(thicknessInInches);
            int wholePart = (int)Math.Floor(thicknessInInches);

            if (Math.Abs(fractionalPart - 0.5) < 0.01)
            {
                return wholePart > 0 ? $"{wholePart} 1/2" : "1/2";
            }
            else if (Math.Abs(fractionalPart - 0.25) < 0.01)
            {
                return wholePart > 0 ? $"{wholePart} 1/4" : "1/4";
            }
            else if (Math.Abs(fractionalPart - 0.75) < 0.01)
            {
                return wholePart > 0 ? $"{wholePart} 3/4" : "3/4";
            }
            else if (Math.Abs(fractionalPart - 0.625) < 0.01)
            {
                return wholePart > 0 ? $"{wholePart} 5/8" : "5/8";
            }
            else if (Math.Abs(fractionalPart - 0.375) < 0.01)
            {
                return wholePart > 0 ? $"{wholePart} 3/8" : "3/8";
            }

            // Default to decimal
            return thicknessInInches.ToString("F1");
        }

        private DB.FloorType CreateFloorTypeWithThickness(DB.FloorType baseFloorType, double thickness, string newTypeName)
        {
            try
            {
                Debug.WriteLine($"Attempting to create floor type '{newTypeName}' with thickness {thickness}ft");

                DB.FloorType newFloorType = baseFloorType.Duplicate(newTypeName) as DB.FloorType;
                if (newFloorType == null)
                {
                    Debug.WriteLine($"Failed to duplicate floor type");
                    return baseFloorType;
                }

                // Try to modify the thickness
                DB.CompoundStructure cs = newFloorType.GetCompoundStructure();
                if (cs != null)
                {
                    Debug.WriteLine($"Original structure has {cs.LayerCount} layers");

                    // Find the core layer (typically the main structural layer)
                    int coreLayerIndex = -1;
                    for (int i = 0; i < cs.LayerCount; i++)
                    {
                        if (cs.GetLayerFunction(i) == DB.MaterialFunctionAssignment.Structure)
                        {
                            coreLayerIndex = i;
                            break;
                        }
                    }

                    if (coreLayerIndex >= 0)
                    {
                        Debug.WriteLine($"Adjusting core layer {coreLayerIndex} thickness to {thickness}ft");
                        cs.SetLayerWidth(coreLayerIndex, thickness);
                        newFloorType.SetCompoundStructure(cs);
                    }
                }

                _allFloorTypes.Add(newFloorType);
                Debug.WriteLine($"Successfully created floor type: '{newFloorType.Name}'");
                return newFloorType;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating floor type: {ex.Message}");
                return baseFloorType;
            }
        }

        private DB.FloorType FindAnyFloorTypeWithStructuralDeck()
        {
            try
            {
                Debug.WriteLine($"Searching for floor types with StructuralDeck layers...");

                foreach (var floorType in _allFloorTypes)
                {
                    try
                    {
                        DB.CompoundStructure cs = floorType.GetCompoundStructure();
                        if (cs != null && cs.HasStructuralDeck)
                        {
                            Debug.WriteLine($"Found StructuralDeck layer in: '{floorType.Name}'");
                            return floorType;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking floor type '{floorType.Name}': {ex.Message}");
                        continue;
                    }
                }

                Debug.WriteLine($"No floor type found with StructuralDeck layer");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in FindAnyFloorTypeWithStructuralDeck: {ex.Message}");
                return null;
            }
        }

        private DB.FloorType CreateFilledDeckFloorType(DB.FloorType baseFloorType, string newTypeName)
        {
            try
            {
                Debug.WriteLine($"Creating FilledDeck floor type '{newTypeName}' from base '{baseFloorType.Name}'");

                // Check if the type already exists
                var existingType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.Equals(newTypeName, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"Floor type '{newTypeName}' already exists, using existing");
                    return existingType;
                }

                DB.FloorType newFloorType = baseFloorType.Duplicate(newTypeName) as DB.FloorType;
                if (newFloorType == null)
                {
                    Debug.WriteLine($"Failed to duplicate base floor type");
                    return baseFloorType;
                }

                // Modify the compound structure for filled deck using class fields
                DB.CompoundStructure cs = newFloorType.GetCompoundStructure();
                if (cs != null && cs.HasStructuralDeck)
                {
                    Debug.WriteLine($"Target thicknesses: Total={_thicknessInFeet:F3}ft, Concrete={_concreteThicknessInFeet:F3}ft, Deck={_deckThicknessInFeet:F3}ft");

                    if (_concreteThicknessInFeet <= 0)
                    {
                        Debug.WriteLine($"Invalid concrete thickness, cannot modify structure");
                        return baseFloorType;
                    }

                    // Find the concrete layer (typically the core layer)
                    int concreteLayerIndex = FindConcreteLayer(cs);
                    if (concreteLayerIndex >= 0)
                    {
                        Debug.WriteLine($"Adjusting concrete layer {concreteLayerIndex} thickness to {_concreteThicknessInFeet:F3}ft");
                        cs.SetLayerWidth(concreteLayerIndex, _concreteThicknessInFeet);
                    }

                    // Find and adjust deck layer if possible
                    int deckLayerIndex = FindStructuralDeckLayer(cs);
                    if (deckLayerIndex >= 0)
                    {
                        Debug.WriteLine($"Adjusting deck layer {deckLayerIndex} thickness to {_deckThicknessInFeet:F3}ft");
                        cs.SetLayerWidth(deckLayerIndex, Math.Max(_deckThicknessInFeet, 0.01));
                    }

                    newFloorType.SetCompoundStructure(cs);
                }

                _allFloorTypes.Add(newFloorType);
                Debug.WriteLine($"Successfully created FilledDeck floor type: '{newFloorType.Name}'");
                return newFloorType;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating FilledDeck floor type: {ex.Message}");
                return baseFloorType;
            }
        }

        private DB.FloorType CreateUnfilledDeckFloorType(string newTypeName)
        {
            try
            {
                Debug.WriteLine($"Creating unfilled deck type: '{newTypeName}'");

                // Find appropriate deck profile
                var deckProfile = FindDeckProfile();
                if (deckProfile == null)
                {
                    Debug.WriteLine("No suitable deck profile found, reverting to slab type");
                    return FindOrCreateSlabType();
                }

                // Find base floor type with structural deck
                var baseFloorType = FindAnyFloorTypeWithStructuralDeck();
                if (baseFloorType == null)
                {
                    Debug.WriteLine("No base floor type with structural deck found");
                    return null;
                }

                // Check if the type already exists
                var existingType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.Equals(newTypeName, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"Unfilled deck type '{newTypeName}' already exists, using existing");
                    return existingType;
                }

                // Duplicate the base type
                DB.FloorType newFloorType = baseFloorType.Duplicate(newTypeName) as DB.FloorType;
                if (newFloorType == null)
                {
                    Debug.WriteLine($"Failed to duplicate base floor type");
                    return baseFloorType;
                }

                // Modify compound structure for unfilled deck (deck layer only)
                if (ModifyUnfilledDeckStructure(newFloorType, deckProfile))
                {
                    _allFloorTypes.Add(newFloorType);
                    Debug.WriteLine($"Successfully created unfilled deck type: '{newFloorType.Name}'");
                    return newFloorType;
                }
                else
                {
                    Debug.WriteLine("Failed to modify deck structure, using base type");
                    return baseFloorType;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating unfilled deck type: {ex.Message}");
                return _defaultDeckType ?? _defaultFloorType;
            }
        }

        private DB.ElementType FindDeckProfile()
        {
            try
            {
                Debug.WriteLine("Searching for deck profiles...");

                // Get all structural deck profiles
                var collector = new DB.FilteredElementCollector(_doc);
                var profiles = collector.OfClass(typeof(DB.FamilySymbol))
                    .Cast<DB.FamilySymbol>()
                    .Where(fs => fs.Category?.Id?.IntegerValue == (int)DB.BuiltInCategory.OST_ProfileFamilies)
                    .ToList();

                Debug.WriteLine($"Found {profiles.Count} profile families");

                // 1. Try "IMEG_Deck_Indents"
                var imegDeckIndents = profiles.FirstOrDefault(p =>
                    p.FamilyName.Equals("IMEG_Deck_Indents", StringComparison.OrdinalIgnoreCase));

                if (imegDeckIndents != null)
                {
                    var matchingType = FindBestDeckTypeByHeight(imegDeckIndents.Family, false);
                    if (matchingType != null)
                    {
                        Debug.WriteLine($"Found IMEG_Deck_Indents profile: '{matchingType.Name}'");
                        return matchingType;
                    }
                }

                // 2. Try "IMEG_Deck"
                var imegDeck = profiles.FirstOrDefault(p =>
                    p.FamilyName.Equals("IMEG_Deck", StringComparison.OrdinalIgnoreCase));

                if (imegDeck != null)
                {
                    var matchingType = FindBestDeckTypeByHeight(imegDeck.Family, false);
                    if (matchingType != null)
                    {
                        Debug.WriteLine($"Found IMEG_Deck profile: '{matchingType.Name}'");
                        return matchingType;
                    }
                }

                // 3. Try any profile containing "deck"
                var deckProfiles = profiles.Where(p =>
                    p.FamilyName.ToLowerInvariant().Contains("deck")).ToList();

                foreach (var deckProfile in deckProfiles)
                {
                    var matchingType = FindBestDeckTypeByHeight(deckProfile.Family, false);
                    if (matchingType != null)
                    {
                        Debug.WriteLine($"Found deck profile: '{matchingType.Name}' in family '{deckProfile.FamilyName}'");
                        return matchingType;
                    }
                }

                Debug.WriteLine("No suitable deck profile found");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding deck profile: {ex.Message}");
                return null;
            }
        }

        private DB.ElementType FindBestDeckTypeByHeight(DB.Family family, bool isComposite)
        {
            try
            {
                var familySymbols = family.GetFamilySymbolIds()
                    .Select(id => _doc.GetElement(id) as DB.FamilySymbol)
                    .Where(fs => fs != null)
                    .ToList();

                Debug.WriteLine($"Checking {familySymbols.Count} types in family '{family.Name}'");

                // Filter by composite/non-composite
                var filteredTypes = familySymbols.Where(fs =>
                {
                    string typeName = fs.Name.ToLowerInvariant();
                    if (isComposite)
                        return typeName.Contains("composite") && !typeName.Contains("non-composite");
                    else
                        return typeName.Contains("non-composite") || (!typeName.Contains("composite"));
                }).ToList();

                if (!filteredTypes.Any())
                {
                    Debug.WriteLine($"No {(isComposite ? "composite" : "non-composite")} types found, using all types");
                    filteredTypes = familySymbols;
                }

                // Find best match by height parameter using class field
                DB.ElementType bestMatch = null;
                double bestDifference = double.MaxValue;

                foreach (var type in filteredTypes)
                {
                    var heightParam = FindHeightParameter(type);
                    if (heightParam != null && heightParam.HasValue)
                    {
                        double paramValueInches = heightParam.AsDouble() * 12.0; // Convert feet to inches
                        double difference = Math.Abs(paramValueInches - _deckThicknessInInches);

                        Debug.WriteLine($"Type '{type.Name}': height = {paramValueInches:F2}\" (target: {_deckThicknessInInches:F2}\"), diff = {difference:F2}");

                        if (difference < bestDifference)
                        {
                            bestDifference = difference;
                            bestMatch = type;
                        }
                    }
                }

                if (bestMatch != null)
                {
                    Debug.WriteLine($"Best match: '{bestMatch.Name}' with height difference {bestDifference:F2}\"");
                }

                return bestMatch;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding best deck type by height: {ex.Message}");
                return null;
            }
        }

        private DB.Parameter FindHeightParameter(DB.ElementType elementType)
        {
            try
            {
                // Look for parameters containing "hr" or "height"
                foreach (DB.Parameter param in elementType.Parameters)
                {
                    if (param.Definition?.Name != null)
                    {
                        string paramName = param.Definition.Name.ToLowerInvariant();
                        if ((paramName.Contains("hr") || paramName.Contains("height")) &&
                            param.StorageType == DB.StorageType.Double &&
                            param.HasValue)
                        {
                            Debug.WriteLine($"Found height parameter: '{param.Definition.Name}' = {param.AsDouble() * 12:F2}\"");
                            return param;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding height parameter: {ex.Message}");
                return null;
            }
        }

        private bool ModifyUnfilledDeckStructure(DB.FloorType floorType, DB.ElementType deckProfile)
        {
            try
            {
                DB.CompoundStructure cs = floorType.GetCompoundStructure();
                if (cs == null)
                {
                    Debug.WriteLine("No compound structure found");
                    return false;
                }

                // For unfilled deck, we want only the structural deck layer
                // Remove all non-deck layers and keep only the structural deck
                var layersToRemove = new List<int>();

                for (int i = 0; i < cs.LayerCount; i++)
                {
                    var function = cs.GetLayerFunction(i);
                    if (function != DB.MaterialFunctionAssignment.StructuralDeck)
                    {
                        layersToRemove.Add(i);
                    }
                }

                // Remove layers in reverse order to maintain indices
                for (int i = layersToRemove.Count - 1; i >= 0; i--)
                {
                    cs.DeleteLayer(layersToRemove[i]);
                }

                // Set the deck thickness and profile using class fields
                if (cs.LayerCount > 0)
                {
                    cs.SetLayerWidth(0, _deckThicknessInFeet);

                    // Try to set the deck profile if available
                    if (deckProfile is DB.FamilySymbol deckSymbol)
                    {
                        cs.SetDeckProfileId(0, deckSymbol.Id);
                        Debug.WriteLine($"Set deck profile to: '{deckSymbol.Name}'");
                    }
                }

                floorType.SetCompoundStructure(cs);
                Debug.WriteLine("Successfully modified unfilled deck structure");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error modifying unfilled deck structure: {ex.Message}");
                return false;
            }
        }

        private int FindConcreteLayer(DB.CompoundStructure cs)
        {
            for (int i = 0; i < cs.LayerCount; i++)
            {
                if (cs.GetLayerFunction(i) == DB.MaterialFunctionAssignment.Structure)
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindStructuralDeckLayer(DB.CompoundStructure cs)
        {
            for (int i = 0; i < cs.LayerCount; i++)
            {
                if (cs.GetLayerFunction(i) == DB.MaterialFunctionAssignment.StructuralDeck)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
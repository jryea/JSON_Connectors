using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Core.Models.Properties;
using Revit.Utilities;
using System.Diagnostics;

namespace Revit.Import.Elements
{
    public class FloorImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FloorType> _floorTypeCache;
        private DB.FloorType _defaultConcreteType;
        private DB.FloorType _defaultDeckType;
        private DB.FloorType _defaultFloorType;
        private List<DB.FloorType> _allFloorTypes;

        public FloorImport(DB.Document doc)
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

        public int Import(Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;
            var floors = model.Elements.Floors;

            // Enhanced validation with better error messages
            if (floors == null || floors.Count == 0)
            {
                Debug.WriteLine("No floors to import");
                return 0;
            }

            if (_allFloorTypes == null || _allFloorTypes.Count == 0)
            {
                Debug.WriteLine("ERROR: No floor types available in Revit document. Cannot import floors.");
                return 0;
            }

            Debug.WriteLine($"Starting import of {floors.Count} floors using fallback strategy");

            foreach (var jsonFloor in floors)
            {
                try
                {
                    // Enhanced validation with detailed logging
                    if (jsonFloor.Points == null || jsonFloor.Points.Count < 3)
                    {
                        Debug.WriteLine($"Skipping floor {jsonFloor.Id}: insufficient points ({jsonFloor.Points?.Count ?? 0})");
                        continue;
                    }

                    // Get level for this floor
                    if (!levelIdMap.TryGetValue(jsonFloor.LevelId, out DB.ElementId levelId))
                    {
                        Debug.WriteLine($"Skipping floor {jsonFloor.Id}: level '{jsonFloor.LevelId}' not found in mapping");
                        continue;
                    }

                    // Get the Level from the ID
                    DB.Level level = _doc.GetElement(levelId) as DB.Level;
                    if (level == null)
                    {
                        Debug.WriteLine($"Skipping floor {jsonFloor.Id}: level element not found");
                        continue;
                    }

                    // Get floor type for this floor using robust fallback strategy
                    DB.FloorType floorType = GetFloorTypeWithFallback(jsonFloor, model);
                    if (floorType == null)
                    {
                        Debug.WriteLine($"Skipping floor {jsonFloor.Id}: no suitable floor type found");
                        continue;
                    }

                    // Create a curve loop for the floor boundary
                    DB.CurveLoop floorLoop = CreateFloorBoundary(jsonFloor);
                    if (floorLoop == null)
                    {
                        Debug.WriteLine($"Skipping floor {jsonFloor.Id}: failed to create boundary");
                        continue;
                    }

                    // Create list of curve loops
                    List<DB.CurveLoop> floorBoundary = new List<DB.CurveLoop> { floorLoop };

                    // Create the floor
                    DB.Floor floor = DB.Floor.Create(_doc, floorBoundary, floorType.Id, levelId);
                    if (floor == null)
                    {
                        Debug.WriteLine($"Skipping floor {jsonFloor.Id}: Revit Floor.Create failed");
                        continue;
                    }

                    // Set the is Structural parameter to true
                    DB.Parameter structuralParam = floor.get_Parameter(DB.BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                    if (structuralParam != null && !structuralParam.IsReadOnly)
                    {
                        structuralParam.Set(1); // 1 means true for integer parameters
                    }

                    count++;
                    Debug.WriteLine($"✓ Created floor {jsonFloor.Id} with type '{floorType.Name}' at level '{level.Name}'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ERROR creating floor {jsonFloor.Id}: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }

            Debug.WriteLine($"Floor import completed: {count} floors created successfully");
            return count;
        }

        /// <summary>
        /// Robust floor type selection with multiple fallback strategies
        /// </summary>
        private DB.FloorType GetFloorTypeWithFallback(CE.Floor jsonFloor, BaseModel model)
        {
            string cacheKey = jsonFloor.FloorPropertiesId ?? "default";

            // Check cache first
            if (_floorTypeCache.TryGetValue(cacheKey, out DB.FloorType cachedType))
            {
                Debug.WriteLine($"Using cached floor type '{cachedType.Name}' for floor {jsonFloor.Id}");
                return cachedType;
            }

            DB.FloorType selectedType = null;
            string selectionReason = "";

            // Strategy 1: Try to match floor properties properly
            try
            {
                selectedType = GetFloorTypeFromProperties(jsonFloor, model);
                if (selectedType != null)
                {
                    // Get the floor properties to show more detail in the log
                    var floorProps = model.Properties?.FloorProperties?
                        .FirstOrDefault(fp => fp.Id == jsonFloor.FloorPropertiesId);

                    string floorTypeInfo = floorProps != null ?
                        $"Type: {floorProps.Type}, Thickness: {floorProps.Thickness}\"" :
                        "Unknown properties";

                    selectionReason = $"matched from floor properties ({jsonFloor.FloorPropertiesId}) - {floorTypeInfo}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Floor property matching failed: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            // Strategy 2: FALLBACK - Use first concrete type (as requested)
            if (selectedType == null && _defaultConcreteType != null)
            {
                selectedType = _defaultConcreteType;
                selectionReason = "fallback to first concrete type";
            }

            // Strategy 3: FALLBACK - Search for any type with "Concrete" in name
            if (selectedType == null)
            {
                selectedType = _allFloorTypes.FirstOrDefault(ft =>
                    ft.Name.ToLowerInvariant().Contains("concrete"));

                if (selectedType != null)
                {
                    selectionReason = "fallback to any type containing 'Concrete'";
                }
            }

            // Strategy 4: FALLBACK - Use any available floor type
            if (selectedType == null)
            {
                selectedType = _defaultFloorType ?? _allFloorTypes.FirstOrDefault();
                if (selectedType != null)
                {
                    selectionReason = "fallback to any available floor type";
                }
            }

            // Final check
            if (selectedType == null)
            {
                Debug.WriteLine($"CRITICAL ERROR: No floor types available in document for floor {jsonFloor.Id}");
                return null;
            }

            Debug.WriteLine($"Selected floor type '{selectedType.Name}' for floor {jsonFloor.Id} ({selectionReason})");

            // Cache the result
            _floorTypeCache[cacheKey] = selectedType;
            return selectedType;
        }

        /// <summary>
        /// Attempts to find floor type based on floor properties (original logic)
        /// </summary>
        private DB.FloorType GetFloorTypeFromProperties(CE.Floor jsonFloor, BaseModel model)
        {
            // Find floor properties in model
            var floorProps = model.Properties?.FloorProperties?
                .FirstOrDefault(fp => fp.Id == jsonFloor.FloorPropertiesId);

            if (floorProps == null)
            {
                Debug.WriteLine($"No floor properties found for ID: {jsonFloor.FloorPropertiesId}");
                return null;
            }

            DB.FloorType floorType = null;

            // Use the Type enum to determine the appropriate Revit floor type
            if (floorProps.Type == StructuralFloorType.Slab)
            {
                // For slabs, try to find concrete type with matching thickness
                string thicknessStr = $"{floorProps.Thickness}\"";
                string typeName = $"{thicknessStr} Concrete";

                // Look for existing type with matching name
                floorType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.Trim().Equals(typeName, StringComparison.OrdinalIgnoreCase));

                if (floorType == null && _defaultConcreteType != null)
                {
                    // Try to create a new type with the right thickness
                    floorType = CreateFloorTypeWithThickness(_defaultConcreteType, floorProps.Thickness, typeName);
                }
            }
            else if (floorProps.Type == StructuralFloorType.FilledDeck)
            {
                // For filled deck: "<ConcreteThickness>" Concrete on <DeckThickness>" Metal Deck"
                floorType = CreateOrFindFilledDeckFloorType(floorProps);
            }
            else if (floorProps.Type == StructuralFloorType.UnfilledDeck)
            {
                // For unfilled deck, try to find a deck type without concrete
                floorType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.ToLowerInvariant().Contains("deck") &&
                                        !ft.Name.ToLowerInvariant().Contains("concrete"));
            }

            return floorType;
        }

        /// <summary>
        /// Creates or finds the correct Revit floor type for FilledDeck floor properties
        /// </summary>
        private DB.FloorType CreateOrFindFilledDeckFloorType(Core.Models.Properties.FloorProperties floorProps)
        {
            try
            {
                // Calculate concrete and deck thicknesses
                double deckThickness = floorProps.DeckProperties?.RibDepth ?? 0;
                double concreteThickness = floorProps.Thickness - deckThickness;

                Debug.WriteLine($"FilledDeck calculation for {floorProps.Name}:");
                Debug.WriteLine($"  Total thickness: {floorProps.Thickness}\"");
                Debug.WriteLine($"  Deck rib depth: {deckThickness}\"");
                Debug.WriteLine($"  Concrete thickness: {concreteThickness}\"");

                // Validate calculations
                if (concreteThickness <= 0)
                {
                    Debug.WriteLine($"WARNING: Invalid concrete thickness ({concreteThickness}\") for FilledDeck. Using default deck type.");
                    return _defaultDeckType;
                }

                // ENHANCED STRATEGY: Look for floor type with correct StructuralDeck layer
                DB.FloorType deckLayerFloorType = FindFloorTypeWithCorrectDeckLayer(deckThickness);
                if (deckLayerFloorType != null)
                {
                    Debug.WriteLine($"  ✓ Found floor type with correct deck layer: '{deckLayerFloorType.Name}'");
                    return deckLayerFloorType;
                }

                // FALLBACK STRATEGY: Create the expected Revit floor type name
                string expectedTypeName = $"{concreteThickness}\" Concrete on {deckThickness}\" Metal Deck";
                Debug.WriteLine($"  Expected Revit floor type name: '{expectedTypeName}'");

                // Try to find existing floor type with this name
                DB.FloorType existingType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.Trim().Equals(expectedTypeName, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"  ✓ Found existing floor type: '{existingType.Name}'");
                    return existingType;
                }

                // Try variations of the name pattern
                var nameVariations = new[]
                {
                    $"{concreteThickness}\" Concrete on {deckThickness}\" Metal Deck",
                    $"{concreteThickness}\" Concrete on {deckThickness}\" Deck",
                    $"{concreteThickness} Concrete on {deckThickness} Metal Deck", // without quotes
                    $"{concreteThickness} Concrete on {deckThickness} Deck",
                    $"{concreteThickness}\" Concrete {deckThickness}\" Metal Deck", // without "on"
                    $"{concreteThickness}\" Concrete {deckThickness}\" Deck"
                };

                foreach (var variation in nameVariations)
                {
                    existingType = _allFloorTypes
                        .FirstOrDefault(ft => ft.Name.Trim().Equals(variation, StringComparison.OrdinalIgnoreCase));

                    if (existingType != null)
                    {
                        Debug.WriteLine($"  ✓ Found floor type with variation: '{existingType.Name}'");
                        return existingType;
                    }
                }

                // Try to create the floor type if we have a suitable base type
                if (_defaultDeckType != null)
                {
                    Debug.WriteLine($"  Attempting to create new floor type: '{expectedTypeName}'");
                    DB.FloorType newType = CreateFloorTypeWithThickness(_defaultDeckType, floorProps.Thickness, expectedTypeName);
                    if (newType != null)
                    {
                        Debug.WriteLine($"  ✓ Created new floor type: '{newType.Name}'");
                        return newType;
                    }
                }

                // ENHANCED STRATEGY: Find any floor type with StructuralDeck layer for duplication
                Debug.WriteLine($"  Searching for any floor type with StructuralDeck layer to use as duplication base...");
                DB.FloorType deckBaseType = FindAnyFloorTypeWithStructuralDeck();
                if (deckBaseType != null)
                {
                    Debug.WriteLine($"  Found deck base type for duplication: '{deckBaseType.Name}'");
                    DB.FloorType newDeckType = CreateFilledDeckFloorType(deckBaseType, floorProps, expectedTypeName);
                    if (newDeckType != null)
                    {
                        Debug.WriteLine($"  ✓ Created FilledDeck floor type: '{newDeckType.Name}'");
                        return newDeckType;
                    }
                }

                // If all else fails, search for any floor type that contains both "concrete" and "deck"
                Debug.WriteLine($"  Searching for any concrete deck floor type...");
                var fallbackType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.ToLowerInvariant().Contains("concrete") &&
                                         ft.Name.ToLowerInvariant().Contains("deck"));

                if (fallbackType != null)
                {
                    Debug.WriteLine($"  ✓ Using fallback concrete deck type: '{fallbackType.Name}'");
                    return fallbackType;
                }

                // Final fallback to default deck type
                Debug.WriteLine($"  Using default deck type as final fallback: '{_defaultDeckType?.Name ?? "None"}'");
                return _defaultDeckType;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in CreateOrFindFilledDeckFloorType: {ex.Message}");
                return _defaultDeckType;
            }
        }

        /// <summary>
        /// Finds a floor type that has a StructuralDeck layer with the correct IMEG_Deck_Indents profile
        /// </summary>
        private DB.FloorType FindFloorTypeWithCorrectDeckLayer(double deckThickness)
        {
            try
            {
                Debug.WriteLine($"  Searching for floor type with StructuralDeck layer matching {deckThickness}\" thickness...");

                // Determine the expected deck type pattern based on thickness
                string expectedDeckPattern = GetExpectedDeckPattern(deckThickness);
                if (string.IsNullOrEmpty(expectedDeckPattern))
                {
                    Debug.WriteLine($"  No deck pattern defined for thickness {deckThickness}\"");
                    return null;
                }

                Debug.WriteLine($"  Looking for deck profile containing: '{expectedDeckPattern}'");

                // Search through all floor types that have structural deck layers
                foreach (var floorType in _allFloorTypes)
                {
                    try
                    {
                        // Get the compound structure
                        DB.CompoundStructure cs = floorType.GetCompoundStructure();
                        if (cs == null || !cs.HasStructuralDeck)
                            continue;

                        Debug.WriteLine($"    Checking floor type '{floorType.Name}' (has StructuralDeck)");

                        // Check each layer for the specific deck profile we need
                        for (int i = 0; i < cs.LayerCount; i++)
                        {
                            // Check if this layer has a StructuralDeck function
                            if (cs.GetLayerFunction(i) == DB.MaterialFunctionAssignment.StructuralDeck)
                            {
                                // Get the profile information for this layer
                                string profileFamilyName = GetLayerProfileFamilyName(cs, i);
                                string profileTypeName = GetLayerProfileTypeName(cs, i);

                                Debug.WriteLine($"      StructuralDeck layer {i}: Family='{profileFamilyName}', Type='{profileTypeName}'");

                                // Check if this matches our requirements
                                if (IsIMEGDeckProfile(profileFamilyName, profileTypeName, expectedDeckPattern))
                                {
                                    Debug.WriteLine($"      ✓ Found matching deck profile in floor type '{floorType.Name}'");
                                    return floorType;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"    Error checking floor type '{floorType.Name}': {ex.Message}");
                        continue;
                    }
                }

                Debug.WriteLine($"  No floor type found with matching StructuralDeck layer");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in FindFloorTypeWithCorrectDeckLayer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the expected deck pattern based on thickness: 1.5" -> "1.5V", 2" -> "2V", 3" -> "3V"
        /// </summary>
        private string GetExpectedDeckPattern(double deckThickness)
        {
            // Round to nearest 0.5 to handle floating point precision
            double rounded = Math.Round(deckThickness * 2) / 2;

            if (Math.Abs(rounded - 1.5) < 0.1)
                return "1.5V";
            else if (Math.Abs(rounded - 2.0) < 0.1)
                return "2V";
            else if (Math.Abs(rounded - 3.0) < 0.1)
                return "3V";

            // For non-standard thicknesses, try to create a pattern
            if (rounded == Math.Floor(rounded))
                return $"{rounded:F0}V"; // e.g., "4V" for 4"
            else
                return $"{rounded:F1}V"; // e.g., "2.5V" for 2.5"
        }

        /// <summary>
        /// Checks if a compound structure layer has StructuralDeck function
        /// </summary>
        private bool HasStructuralDeckFunction(DB.CompoundStructure cs, int layerIndex)
        {
            try
            {
                // In Revit API, layer functions are determined by the MaterialFunctionAssignment
                // StructuralDeck is one of the possible functions
                var function = cs.GetLayerFunction(layerIndex);
                return function == DB.MaterialFunctionAssignment.StructuralDeck;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"      Error checking layer function: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the profile family name for a compound structure layer
        /// </summary>
        private string GetLayerProfileFamilyName(DB.CompoundStructure cs, int layerIndex)
        {
            try
            {
                // Get the deck profile ID for this layer
                DB.ElementId profileId = cs.GetDeckProfileId(layerIndex);
                if (profileId == null || profileId == DB.ElementId.InvalidElementId)
                    return string.Empty;

                // Get the family symbol
                DB.FamilySymbol profileSymbol = _doc.GetElement(profileId) as DB.FamilySymbol;
                if (profileSymbol == null)
                    return string.Empty;

                return profileSymbol.Family.Name;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"      Error getting deck profile family name: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the profile type name for a compound structure layer
        /// </summary>
        private string GetLayerProfileTypeName(DB.CompoundStructure cs, int layerIndex)
        {
            try
            {
                // Get the deck profile ID for this layer
                DB.ElementId profileId = cs.GetDeckProfileId(layerIndex);
                if (profileId == null || profileId == DB.ElementId.InvalidElementId)
                    return string.Empty;

                // Get the family symbol
                DB.FamilySymbol profileSymbol = _doc.GetElement(profileId) as DB.FamilySymbol;
                if (profileSymbol == null)
                    return string.Empty;

                return profileSymbol.Name;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"      Error getting deck profile type name: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Checks if the profile matches IMEG deck requirements
        /// </summary>
        private bool IsIMEGDeckProfile(string familyName, string typeName, string expectedPattern)
        {
            try
            {
                // Check family name matches "IMEG_Deck_Indents"
                if (string.IsNullOrEmpty(familyName) ||
                    !familyName.Equals("IMEG_Deck_Indents", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Check type name contains the expected pattern (e.g., "1.5V", "2V", "3V")
                if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(expectedPattern))
                    return false;

                bool matches = typeName.ToUpperInvariant().Contains(expectedPattern.ToUpperInvariant());
                Debug.WriteLine($"      Pattern match check: '{typeName}' contains '{expectedPattern}' = {matches}");

                return matches;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"      Error checking IMEG deck profile: {ex.Message}");
                return false;
            }
        }

        private DB.CurveLoop CreateFloorBoundary(CE.Floor jsonFloor)
        {
            try
            {
                DB.CurveLoop floorLoop = new DB.CurveLoop();

                // Convert each point and add to curve loop
                for (int i = 0; i < jsonFloor.Points.Count; i++)
                {
                    DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonFloor.Points[i]);
                    DB.XYZ endPoint;

                    // If last point, connect back to first point
                    if (i == jsonFloor.Points.Count - 1)
                    {
                        endPoint = Helpers.ConvertToRevitCoordinates(jsonFloor.Points[0]);
                    }
                    else
                    {
                        endPoint = Helpers.ConvertToRevitCoordinates(jsonFloor.Points[i + 1]);
                    }

                    // Validate that start and end points are different
                    if (startPoint.DistanceTo(endPoint) < 0.01) // 0.01 feet = ~1/8 inch
                    {
                        Debug.WriteLine($"Skipping zero-length line segment in floor {jsonFloor.Id}");
                        continue;
                    }

                    DB.Line line = DB.Line.CreateBound(startPoint, endPoint);
                    floorLoop.Append(line);
                }

                // Validate that we have a valid curve loop
                if (floorLoop.NumberOfCurves() < 3)
                {
                    Debug.WriteLine($"Invalid floor boundary: only {floorLoop.NumberOfCurves()} valid curves");
                    return null;
                }

                return floorLoop;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating floor boundary: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create a new floor type with specified thickness by duplicating an existing type
        /// </summary>
        private DB.FloorType CreateFloorTypeWithThickness(DB.FloorType baseFloorType, double thickness, string newTypeName)
        {
            try
            {
                Debug.WriteLine($"Attempting to create floor type '{newTypeName}' with thickness {thickness}\"");

                // Check if the type already exists
                var existingType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.Equals(newTypeName, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"Floor type '{newTypeName}' already exists, using existing type");
                    return existingType;
                }

                // Try to duplicate the floor type
                DB.FloorType newFloorType = baseFloorType.Duplicate(newTypeName) as DB.FloorType;
                if (newFloorType == null)
                {
                    Debug.WriteLine($"Failed to duplicate floor type, using base type '{baseFloorType.Name}'");
                    return baseFloorType;
                }

                Debug.WriteLine($"Successfully created new floor type '{newTypeName}'");

                // Add to our collection for future reference
                _allFloorTypes.Add(newFloorType);

                return newFloorType;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating floor type with thickness: {ex.Message}");
                Debug.WriteLine($"Falling back to base type '{baseFloorType.Name}'");
                return baseFloorType; // Return original type if duplication fails
            }
        }

        /// <summary>
        /// Finds any floor type that has a StructuralDeck layer (for use as duplication base)
        /// </summary>
        private DB.FloorType FindAnyFloorTypeWithStructuralDeck()
        {
            try
            {
                Debug.WriteLine($"    Searching through {_allFloorTypes.Count} floor types for StructuralDeck layers...");

                foreach (var floorType in _allFloorTypes)
                {
                    try
                    {
                        DB.CompoundStructure cs = floorType.GetCompoundStructure();
                        if (cs != null && cs.HasStructuralDeck)
                        {
                            Debug.WriteLine($"    Found StructuralDeck layer in floor type: '{floorType.Name}'");
                            return floorType;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"    Error checking floor type '{floorType.Name}': {ex.Message}");
                        continue;
                    }
                }

                Debug.WriteLine($"    No floor type found with StructuralDeck layer");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in FindAnyFloorTypeWithStructuralDeck: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a FilledDeck floor type by duplicating and modifying a base floor type with StructuralDeck
        /// </summary>
        private DB.FloorType CreateFilledDeckFloorType(DB.FloorType baseFloorType, Core.Models.Properties.FloorProperties floorProps, string newTypeName)
        {
            try
            {
                Debug.WriteLine($"    Creating FilledDeck floor type '{newTypeName}' from base '{baseFloorType.Name}'");

                // Check if the type already exists
                var existingType = _allFloorTypes
                    .FirstOrDefault(ft => ft.Name.Equals(newTypeName, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"    Floor type '{newTypeName}' already exists, using existing type");
                    return existingType;
                }

                // Duplicate the floor type
                DB.FloorType newFloorType = baseFloorType.Duplicate(newTypeName) as DB.FloorType;
                if (newFloorType == null)
                {
                    Debug.WriteLine($"    Failed to duplicate base floor type");
                    return baseFloorType;
                }

                // Get and modify the compound structure
                DB.CompoundStructure cs = newFloorType.GetCompoundStructure();
                if (cs != null)
                {
                    bool modified = ModifyCompoundStructureForFilledDeck(cs, floorProps);
                    if (modified)
                    {
                        newFloorType.SetCompoundStructure(cs);
                        Debug.WriteLine($"    ✓ Modified compound structure for FilledDeck requirements");
                    }
                    else
                    {
                        Debug.WriteLine($"    Could not modify compound structure, using as-is");
                    }
                }

                // Add to our collection for future reference
                _allFloorTypes.Add(newFloorType);

                Debug.WriteLine($"    ✓ Successfully created FilledDeck floor type: '{newTypeName}'");
                return newFloorType;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR creating FilledDeck floor type: {ex.Message}");
                return baseFloorType; // Return base type if creation fails
            }
        }

        /// <summary>
        /// Modifies a compound structure to match FilledDeck requirements
        /// </summary>
        private bool ModifyCompoundStructureForFilledDeck(DB.CompoundStructure cs, Core.Models.Properties.FloorProperties floorProps)
        {
            try
            {
                double targetTotalThickness = floorProps.Thickness / 12.0; // Convert to feet
                double deckThickness = (floorProps.DeckProperties?.RibDepth ?? 0) / 12.0; // Convert to feet
                double concreteThickness = targetTotalThickness - deckThickness;

                Debug.WriteLine($"      Target thicknesses: Total={targetTotalThickness:F3}ft, Concrete={concreteThickness:F3}ft, Deck={deckThickness:F3}ft");

                if (concreteThickness <= 0)
                {
                    Debug.WriteLine($"      Invalid concrete thickness, cannot modify structure");
                    return false;
                }

                // Find the concrete layer (typically the core layer)
                int concreteLayerIndex = FindConcreteLayer(cs);
                if (concreteLayerIndex >= 0)
                {
                    Debug.WriteLine($"      Adjusting concrete layer {concreteLayerIndex} thickness to {concreteThickness:F3}ft");
                    cs.SetLayerWidth(concreteLayerIndex, concreteThickness);
                }

                // Find and adjust deck layer if possible
                int deckLayerIndex = FindStructuralDeckLayer(cs);
                if (deckLayerIndex >= 0)
                {
                    Debug.WriteLine($"      Adjusting deck layer {deckLayerIndex} thickness to {deckThickness:F3}ft");
                    cs.SetLayerWidth(deckLayerIndex, Math.Max(deckThickness, 0.01)); // Minimum thickness

                    // Optionally try to update the deck profile
                    TryUpdateDeckProfile(cs, deckLayerIndex, floorProps.DeckProperties?.RibDepth ?? 0);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"      ERROR modifying compound structure: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds the concrete layer in a compound structure
        /// </summary>
        private int FindConcreteLayer(DB.CompoundStructure cs)
        {
            try
            {
                // First try to find the core layer
                int coreLayerIndex = cs.GetFirstCoreLayerIndex();
                if (coreLayerIndex >= 0)
                {
                    return coreLayerIndex;
                }

                // If no core layer, look for concrete material
                for (int i = 0; i < cs.LayerCount; i++)
                {
                    DB.ElementId materialId = cs.GetMaterialId(i);
                    if (materialId != DB.ElementId.InvalidElementId)
                    {
                        DB.Material material = _doc.GetElement(materialId) as DB.Material;
                        if (material != null && material.Name.ToLowerInvariant().Contains("concrete"))
                        {
                            return i;
                        }
                    }
                }

                // If still no match, return the thickest layer
                int thickestLayer = 0;
                double maxThickness = 0;
                for (int i = 0; i < cs.LayerCount; i++)
                {
                    double thickness = cs.GetLayerWidth(i);
                    if (thickness > maxThickness)
                    {
                        maxThickness = thickness;
                        thickestLayer = i;
                    }
                }

                return thickestLayer;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"        ERROR finding concrete layer: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Finds the StructuralDeck layer in a compound structure
        /// </summary>
        private int FindStructuralDeckLayer(DB.CompoundStructure cs)
        {
            try
            {
                for (int i = 0; i < cs.LayerCount; i++)
                {
                    if (HasStructuralDeckFunction(cs, i))
                    {
                        return i;
                    }
                }
                return -1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"        ERROR finding structural deck layer: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Attempts to update the deck profile to match the required thickness
        /// </summary>
        private void TryUpdateDeckProfile(DB.CompoundStructure cs, int deckLayerIndex, double deckThickness)
        {
            try
            {
                string expectedPattern = GetExpectedDeckPattern(deckThickness);
                if (string.IsNullOrEmpty(expectedPattern))
                    return;

                Debug.WriteLine($"        Attempting to update deck profile to match pattern: '{expectedPattern}'");

                // Get all IMEG_Deck_Indents family symbols
                var deckProfiles = new DB.FilteredElementCollector(_doc)
                    .OfClass(typeof(DB.FamilySymbol))
                    .Cast<DB.FamilySymbol>()
                    .Where(fs => fs.Family.Name.Equals("IMEG_Deck_Indents", StringComparison.OrdinalIgnoreCase))
                    .Where(fs => fs.Name.ToUpperInvariant().Contains(expectedPattern.ToUpperInvariant()))
                    .ToList();

                if (deckProfiles.Any())
                {
                    var matchingProfile = deckProfiles.First();
                    Debug.WriteLine($"        Found matching deck profile: '{matchingProfile.Name}'");

                    // Set the deck profile for this layer using the correct method
                    cs.SetDeckProfileId(deckLayerIndex, matchingProfile.Id);
                    Debug.WriteLine($"        ✓ Updated deck layer profile to '{matchingProfile.Name}'");
                }
                else
                {
                    Debug.WriteLine($"        No matching IMEG deck profile found for pattern '{expectedPattern}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"        ERROR updating deck profile: {ex.Message}");
            }
        }
    }
}
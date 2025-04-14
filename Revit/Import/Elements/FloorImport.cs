using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Revit.Utilities;
using Autodesk.Revit.UI;
using System.Diagnostics;
using Core.Models.Properties;
using System.Net;
using Core.Models;

namespace Revit.Import.Elements
{
    // Imports floor elements from JSON into Revit
    public class FloorImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.ElementId> _floorPropertyIdMap;

        public FloorImport(DB.Document doc)
        {
            _doc = doc;
            _floorPropertyIdMap = new Dictionary<string, DB.ElementId>();
        }

        public int Import(Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;
            var floors = model.Elements.Floors;

            if (floors == null || floors.Count == 0)
                return 0;

            // Create floor property mappings before processing floors
            CreateFloorPropertyMappings(model.Properties.FloorProperties);

            // Check if any floor types exist
            DB.FilteredElementCollector floorTypeCollector = new DB.FilteredElementCollector(_doc);
            floorTypeCollector.OfClass(typeof(DB.FloorType));
            List<DB.FloorType> existingFloorTypes = floorTypeCollector.Cast<DB.FloorType>().ToList();

            if (existingFloorTypes.Count == 0)
            {
                TaskDialog.Show("Import Error", "No floor types found in the document. Cannot import floors.");
                return 0;
            }

            // Create collections of concrete and metal deck floor types
            List<DB.FloorType> concreteFloorTypes = existingFloorTypes
                .Where(ft => ft.Name.Contains("Concrete"))
                .ToList();

            List<DB.FloorType> deckFloorTypes = existingFloorTypes
                .Where(ft => ft.Name.Contains("Metal Deck") || ft.Name.Contains("Deck"))
                .ToList();

            // Preferred deck type - "3" Concrete on 2" Metal Deck"
            DB.FloorType preferredDeckType = deckFloorTypes.FirstOrDefault(ft =>
                ft.Name.Contains("3") && ft.Name.Contains("Concrete") &&
                ft.Name.Contains("Metal Deck"));

            // Default deck type - any metal deck type
            DB.FloorType defaultDeckType = preferredDeckType ??
                                          deckFloorTypes.FirstOrDefault();

            // Default concrete type
            DB.FloorType defaultConcreteType = concreteFloorTypes.FirstOrDefault(ft =>
                ft.Name.Contains("6\"")) ??
                concreteFloorTypes.FirstOrDefault() ??
                existingFloorTypes.FirstOrDefault();

            Debug.WriteLine($"Found {deckFloorTypes.Count} deck floor types");
            foreach (var dt in deckFloorTypes)
            {
                Debug.WriteLine($"Available deck type: {dt.Name}");
            }

            Debug.WriteLine($"Preferred deck type: {(preferredDeckType != null ? preferredDeckType.Name : "None found")}");

            foreach (var jsonFloor in floors)
            {
                try
                {
                    // Skip floors with less than 3 points
                    if (jsonFloor.Points == null || jsonFloor.Points.Count < 3)
                        continue;

                    // Get level for this floor
                    if (!levelIdMap.TryGetValue(jsonFloor.LevelId, out DB.ElementId levelId))
                        continue;

                    // Get the Level from the ID
                    DB.Level level = _doc.GetElement(levelId) as DB.Level;
                    if (level == null)
                        continue;

                    // Get floor properties
                    DB.FloorType floorType = null;

                    // Use existing mapping if available
                    if (_floorPropertyIdMap.TryGetValue(jsonFloor.FloorPropertiesId, out DB.ElementId floorTypeId))
                    {
                        floorType = _doc.GetElement(floorTypeId) as DB.FloorType;
                    }
                    else
                    {
                        // Find floor properties in model
                        var floorProps = model.Properties.FloorProperties
                            .FirstOrDefault(fp => fp.Id == jsonFloor.FloorPropertiesId);

                        if (floorProps != null)
                        {
                            double thickness = floorProps.Thickness;
                            string thicknessStr = $"{thickness}\"";
                            string floorTypeStr = floorProps.Type?.ToLower() ?? "";

                            // For 'Composite' and 'NonComposite' types, use metal deck types
                            if (floorTypeStr == "composite" || floorTypeStr == "noncomposite")
                            {
                                // Use preferred deck type first, then any metal deck, then concrete as fallback
                                floorType = defaultDeckType ?? defaultConcreteType;
                            }
                            if (floorTypeStr == "slab")
                            {
                                // Format thickness string correctly
                                string desiredTypeName = $"{thicknessStr} Concrete";

                                // Use case-insensitive comparison and trim spaces
                                floorType = concreteFloorTypes.FirstOrDefault(ft =>
                                    ft.Name.Trim().Equals(desiredTypeName, StringComparison.OrdinalIgnoreCase));

                                Debug.WriteLine($"Looking for floor type '{desiredTypeName}' - Found: {(floorType != null ? "Yes" : "No")}");

                                // If no matching type exists, create one
                                if (floorType == null && defaultConcreteType != null)
                                {
                                    Debug.WriteLine($"Creating new floor type '{desiredTypeName}' from {defaultConcreteType.Name}");
                                    floorType = CreateFloorTypeWithThickness(defaultConcreteType, thickness, desiredTypeName);

                                    // Double-check if duplication succeeded
                                    if (floorType.Id == defaultConcreteType.Id)
                                    {
                                        Debug.WriteLine("WARNING: Floor type duplication failed - using default type instead");
                                    }
                                }
                                else if (floorType == null)
                                {
                                    Debug.WriteLine($"No matching floor type found and cannot create one - using default concrete type");
                                    floorType = defaultConcreteType;
                                }
                            }
                            else
                            {
                                // Default to concrete type for unknown types
                                floorType = defaultConcreteType;
                            }
                        }
                        else
                        {
                            // Fallback to default concrete type if no properties found
                            floorType = defaultConcreteType;
                        }

                        // Update mapping for future floors
                        if (floorType != null && !string.IsNullOrEmpty(jsonFloor.FloorPropertiesId))
                        {
                            _floorPropertyIdMap[jsonFloor.FloorPropertiesId] = floorType.Id;
                        }
                    }

                    // Skip if no floor type is available
                    if (floorType == null)
                        continue;

                    // Create a curve loop for the floor boundary
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

                        DB.Line line = DB.Line.CreateBound(startPoint, endPoint);
                        floorLoop.Append(line);
                    }

                    // Create list of curve loops
                    List<DB.CurveLoop> floorBoundary = new List<DB.CurveLoop> { floorLoop };

                    // Create the floor
                    DB.Floor floor = DB.Floor.Create(_doc, floorBoundary, floorType.Id, levelId);

                    // Set the is Structural parameter to true
                    DB.Parameter structuralParam = floor.get_Parameter(DB.BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                    if (structuralParam != null && !structuralParam.IsReadOnly)
                    {
                        structuralParam.Set(1); // 1 means true for integer parameters
                    }

                    if (floor != null)
                        count++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating floor {jsonFloor.Id}: {ex.Message}");
                }
            }

            return count;
        }

        // Method for creating floor property mappings
        private void CreateFloorPropertyMappings(List<FloorProperties> floorProperties)
        {
            _floorPropertyIdMap.Clear();

            if (floorProperties == null || floorProperties.Count == 0)
                return;

            // Collect all floor types
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.FloorType));
            List<DB.FloorType> floorTypes = collector.Cast<DB.FloorType>().ToList();

            if (floorTypes.Count == 0)
                return;

            // Sort floor types into categories
            List<DB.FloorType> concreteTypes = floorTypes
                .Where(ft => ft.Name.Contains("Concrete") && !ft.Name.Contains("Metal"))
                .ToList();

            List<DB.FloorType> deckTypes = floorTypes
                .Where(ft => ft.Name.Contains("Metal Deck") || ft.Name.Contains("Deck"))
                .ToList();

            // Get preferred deck type
            DB.FloorType preferredDeckType = deckTypes.FirstOrDefault(ft =>
                ft.Name.Contains("3") && ft.Name.Contains("Concrete") &&
                ft.Name.Contains("Metal Deck"));

            // Default floor types
            DB.FloorType defaultDeckType = preferredDeckType ?? deckTypes.FirstOrDefault();
            DB.FloorType defaultConcreteType = concreteTypes.FirstOrDefault();
            DB.FloorType defaultType = defaultConcreteType ?? floorTypes.FirstOrDefault();

            // Map each floor property to a floor type
            foreach (FloorProperties prop in floorProperties)
            {
                DB.FloorType matchedType = null;

                // Determine type based on floor property type
                string floorTypeStr = prop.Type?.ToLower() ?? "";

                if (floorTypeStr == "composite" || floorTypeStr == "noncomposite")
                {
                    // Use deck type for composite floors
                    matchedType = defaultDeckType ?? defaultType;
                }
                else if (floorTypeStr == "slab")
                {
                    // For slabs, find appropriate concrete type
                    string thicknessStr = $"{prop.Thickness}\"";
                    string typeName = $"{thicknessStr} Concrete";

                    matchedType = concreteTypes.FirstOrDefault(ft =>
                        ft.Name.Trim().Equals(typeName, StringComparison.OrdinalIgnoreCase));

                    if (matchedType == null)
                    {
                        matchedType = defaultConcreteType ?? defaultType;
                    }
                }
                else
                {
                    // Default to concrete type
                    matchedType = defaultType;
                }

                // Add to mapping
                if (matchedType != null)
                {
                    _floorPropertyIdMap[prop.Id] = matchedType.Id;
                    Debug.WriteLine($"Mapped floor property '{prop.Name}' of type '{prop.Type}' to floor type '{matchedType.Name}'");
                }
            }
        }

        // Create a new floor type with the specified thickness by duplicating an existing type
        private DB.FloorType CreateFloorTypeWithThickness(DB.FloorType baseFloorType, double thickness, string newTypeName)
        {
            try
            {
                Debug.WriteLine($"Attempting to duplicate floor type '{baseFloorType.Name}' with new name '{newTypeName}'");

                // Duplicate the floor type
                DB.FloorType newFloorType = baseFloorType.Duplicate(newTypeName) as DB.FloorType;

                if (newFloorType == null)
                {
                    Debug.WriteLine("Error: Floor type duplication returned null");
                    return baseFloorType;
                }

                Debug.WriteLine($"Successfully duplicated floor type. New ID: {newFloorType.Id}");

                // Get the compound structure
                DB.CompoundStructure cs = newFloorType.GetCompoundStructure();
                if (cs == null)
                {
                    Debug.WriteLine("Error: New floor type has no compound structure");
                    return newFloorType;
                }

                // Convert inches to feet for Revit
                double thicknessInFeet = thickness / 12.0;

                // Get the core layer (structural) for a concrete slab
                int coreLayerIndex = cs.GetFirstCoreLayerIndex();
                if (coreLayerIndex < 0 || coreLayerIndex >= cs.LayerCount)
                {
                    // If no core layer, try to find a concrete layer
                    for (int i = 0; i < cs.LayerCount; i++)
                    {
                        DB.Material material = _doc.GetElement(cs.GetMaterialId(i)) as DB.Material;
                        if (material != null && material.Name.Contains("Concrete"))
                        {
                            coreLayerIndex = i;
                            break;
                        }
                    }

                    // If still no match, use the thickest layer
                    if (coreLayerIndex < 0 || coreLayerIndex >= cs.LayerCount)
                    {
                        double maxThickness = 0;
                        for (int i = 0; i < cs.LayerCount; i++)
                        {
                            double layerThickness = cs.GetLayerWidth(i);
                            if (layerThickness > maxThickness)
                            {
                                maxThickness = layerThickness;
                                coreLayerIndex = i;
                            }
                        }
                    }
                }

                if (coreLayerIndex >= 0 && coreLayerIndex < cs.LayerCount)
                {
                    // Calculate current total thickness
                    double currentTotalThickness = 0;
                    for (int i = 0; i < cs.LayerCount; i++)
                    {
                        currentTotalThickness += cs.GetLayerWidth(i);
                    }

                    // Get current core thickness
                    double currentCoreThickness = cs.GetLayerWidth(coreLayerIndex);

                    // Calculate thickness difference needed
                    double thicknessDiff = thicknessInFeet - currentTotalThickness;

                    // Modify layer width
                    double newCoreThickness = Math.Max(0.01, currentCoreThickness + thicknessDiff);

                    Debug.WriteLine($"Adjusting core layer {coreLayerIndex} thickness from {currentCoreThickness}ft to {newCoreThickness}ft");
                    Debug.WriteLine($"Total thickness changing from {currentTotalThickness}ft to {thicknessInFeet}ft");

                    cs.SetLayerWidth(coreLayerIndex, newCoreThickness);

                    // Apply changes
                    newFloorType.SetCompoundStructure(cs);

                    Debug.WriteLine($"Successfully created floor type '{newTypeName}' with core thickness {newCoreThickness}ft");
                    return newFloorType;
                }
                else
                {
                    Debug.WriteLine("Error: Could not identify a core layer to modify");
                }

                return newFloorType;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating floor type with thickness {thickness}: {ex.Message}");
                return baseFloorType;
            }
        }
    }
}
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

        public FloorImport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Import(Dictionary<string, DB.ElementId> levelIdMap, Dictionary<string, DB.ElementId> floorPropertyIdMap, BaseModel model)
        {
            int count = 0;
            var floors = model.Elements.Floors;

            if (floors == null || floors.Count == 0)
                return 0;

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
                    if (floorPropertyIdMap.TryGetValue(jsonFloor.FloorPropertiesId, out DB.ElementId floorTypeId))
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
                            else if (floorTypeStr == "slab")
                            {
                                // Look for concrete slab type with matching thickness
                                string floorTypeName = $"{thicknessStr} Concrete";
                                floorType = concreteFloorTypes.FirstOrDefault(ft => ft.Name == floorTypeName);

                                // If no matching type exists, create one
                                if (floorType == null && defaultConcreteType != null)
                                {
                                    floorType = CreateFloorTypeWithThickness(defaultConcreteType, thickness, floorTypeName);
                                }
                                else if (floorType == null)
                                {
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
                            floorPropertyIdMap[jsonFloor.FloorPropertiesId] = floorType.Id;
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

        // Create a new floor type with the specified thickness by duplicating an existing type
        private DB.FloorType CreateFloorTypeWithThickness(DB.FloorType baseFloorType, double thickness, string newTypeName)
        {
            try
            {
                // Duplicate the floor type
                DB.FloorType newFloorType = baseFloorType.Duplicate(newTypeName) as DB.FloorType;

                if (newFloorType == null)
                    return baseFloorType;

                // Get the compound structure
                DB.CompoundStructure cs = newFloorType.GetCompoundStructure();
                if (cs == null)
                    return newFloorType;

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
                    // Convert inches to feet for Revit
                    double thicknessInFeet = thickness / 12.0;

                    // Get current total thickness
                    double currentTotalThickness = 0;
                    for (int i = 0; i < cs.LayerCount; i++)
                    {
                        currentTotalThickness += cs.GetLayerWidth(i);
                    }

                    // Get current core thickness
                    double currentCoreThickness = cs.GetLayerWidth(coreLayerIndex);

                    // Calculate difference to apply
                    double thicknessDiff = thicknessInFeet - currentTotalThickness;

                    // Modify layer width
                    double newCoreThickness = Math.Max(0.01, currentCoreThickness + thicknessDiff);
                    cs.SetLayerWidth(coreLayerIndex, newCoreThickness);

                    // Apply changes
                    newFloorType.SetCompoundStructure(cs);

                    Debug.WriteLine($"Created floor type '{newTypeName}' with core thickness {newCoreThickness}ft");
                    return newFloorType;
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
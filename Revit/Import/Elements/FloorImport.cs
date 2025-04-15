using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Revit.Utilities;
using System.Diagnostics;
using Core.Models;

namespace Revit.Import.Elements
{
    // Imports floor elements from JSON into Revit
    public class FloorImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FloorType> _floorTypeCache;
        private DB.FloorType _defaultConcreteType;
        private DB.FloorType _defaultDeckType;
        private DB.FloorType _defaultFloorType;

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
            List<DB.FloorType> allFloorTypes = collector.OfClass(typeof(DB.FloorType))
                .Cast<DB.FloorType>()
                .ToList();

            if (allFloorTypes.Count == 0)
            {
                Debug.WriteLine("No floor types found in the document");
                return;
            }

            // Categorize floor types
            var concreteTypes = allFloorTypes
                .Where(ft => ft.Name.Contains("Concrete") && !ft.Name.Contains("Metal"))
                .ToList();

            var deckTypes = allFloorTypes
                .Where(ft => ft.Name.Contains("Metal Deck") || ft.Name.Contains("Deck"))
                .ToList();

            // Find preferred deck type (3" Concrete on 2" Metal Deck)
            _defaultDeckType = deckTypes.FirstOrDefault(ft =>
                ft.Name.Contains("3") && ft.Name.Contains("Concrete") &&
                ft.Name.Contains("Metal Deck")) ?? deckTypes.FirstOrDefault();

            // Set default concrete type (preferring 6" concrete if available)
            _defaultConcreteType = concreteTypes.FirstOrDefault(ft => ft.Name.Contains("6\""))
                ?? concreteTypes.FirstOrDefault();

            // Set default fallback type
            _defaultFloorType = _defaultConcreteType ?? _defaultDeckType ?? allFloorTypes.FirstOrDefault();

            Debug.WriteLine($"Default concrete type: {(_defaultConcreteType?.Name ?? "None")}");
            Debug.WriteLine($"Default deck type: {(_defaultDeckType?.Name ?? "None")}");
            Debug.WriteLine($"Default floor type: {(_defaultFloorType?.Name ?? "None")}");
        }

        public int Import(Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;
            var floors = model.Elements.Floors;

            if (floors == null || floors.Count == 0 || _defaultFloorType == null)
                return 0;

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

                    // Get floor type for this floor
                    DB.FloorType floorType = GetFloorTypeForFloor(jsonFloor, model);
                    if (floorType == null)
                        continue;

                    // Create a curve loop for the floor boundary
                    DB.CurveLoop floorLoop = CreateFloorBoundary(jsonFloor);
                    if (floorLoop == null)
                        continue;

                    // Create list of curve loops
                    List<DB.CurveLoop> floorBoundary = new List<DB.CurveLoop> { floorLoop };

                    // Create the floor
                    DB.Floor floor = DB.Floor.Create(_doc, floorBoundary, floorType.Id, levelId);
                    if (floor == null)
                        continue;

                    // Set the is Structural parameter to true
                    DB.Parameter structuralParam = floor.get_Parameter(DB.BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                    if (structuralParam != null && !structuralParam.IsReadOnly)
                    {
                        structuralParam.Set(1); // 1 means true for integer parameters
                    }

                    count++;
                    Debug.WriteLine($"Created floor with type '{floorType.Name}' at level '{level.Name}'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating floor {jsonFloor.Id}: {ex.Message}");
                }
            }

            return count;
        }

        private DB.CurveLoop CreateFloorBoundary(Core.Models.Elements.Floor jsonFloor)
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

                    DB.Line line = DB.Line.CreateBound(startPoint, endPoint);
                    floorLoop.Append(line);
                }

                return floorLoop;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating floor boundary: {ex.Message}");
                return null;
            }
        }

        private DB.FloorType GetFloorTypeForFloor(Core.Models.Elements.Floor jsonFloor, BaseModel model)
        {
            // Check if we already have a cached floor type for this floor property
            string cacheKey = jsonFloor.FloorPropertiesId ?? "default";
            if (_floorTypeCache.TryGetValue(cacheKey, out DB.FloorType cachedType))
                return cachedType;

            // Find floor properties in model
            var floorProps = model.Properties.FloorProperties
                .FirstOrDefault(fp => fp.Id == jsonFloor.FloorPropertiesId);

            if (floorProps == null)
            {
                _floorTypeCache[cacheKey] = _defaultFloorType;
                return _defaultFloorType;
            }

            // Determine floor type based on properties
            DB.FloorType floorType = null;
            string floorTypeStr = floorProps.Type?.ToLower() ?? "";
            double thickness = floorProps.Thickness;

            if (floorTypeStr == "composite" || floorTypeStr == "noncomposite")
            {
                // Use deck type for composite floors
                floorType = _defaultDeckType ?? _defaultFloorType;
            }
            else if (floorTypeStr == "slab")
            {
                // For slabs, find or create appropriate concrete type
                string thicknessStr = $"{thickness}\"";
                string typeName = $"{thicknessStr} Concrete";

                // Look for existing type with matching name
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                floorType = collector.OfClass(typeof(DB.FloorType))
                    .Cast<DB.FloorType>()
                    .FirstOrDefault(ft => ft.Name.Trim().Equals(typeName, StringComparison.OrdinalIgnoreCase));

                if (floorType == null && _defaultConcreteType != null)
                {
                    // Try to create a new type with the right thickness
                    floorType = CreateFloorTypeWithThickness(_defaultConcreteType, thickness, typeName);
                }

                if (floorType == null)
                {
                    floorType = _defaultConcreteType ?? _defaultFloorType;
                }
            }
            else
            {
                // Default case
                floorType = _defaultFloorType;
            }

            // Cache the result for future lookups
            _floorTypeCache[cacheKey] = floorType;
            return floorType;
        }

        // Create a new floor type with the specified thickness by duplicating an existing type
        private DB.FloorType CreateFloorTypeWithThickness(DB.FloorType baseFloorType, double thickness, string newTypeName)
        {
            try
            {
                Debug.WriteLine($"Creating floor type '{newTypeName}' with thickness {thickness}\"");

                // Check if the type already exists
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                var existingType = collector.OfClass(typeof(DB.FloorType))
                    .Cast<DB.FloorType>()
                    .FirstOrDefault(ft => ft.Name.Equals(newTypeName, StringComparison.OrdinalIgnoreCase));

                if (existingType != null)
                {
                    Debug.WriteLine($"Floor type '{newTypeName}' already exists, using that");
                    return existingType;
                }

                // Duplicate the floor type
                DB.FloorType newFloorType = baseFloorType.Duplicate(newTypeName) as DB.FloorType;
                if (newFloorType == null)
                    return baseFloorType;

                // Get the compound structure
                DB.CompoundStructure cs = newFloorType.GetCompoundStructure();
                if (cs == null)
                    return newFloorType;

                // Convert inches to feet for Revit
                double thicknessInFeet = thickness / 12.0;

                // Assuming single layer floor - set the thickness directly
                if (cs.LayerCount == 1)
                {
                    cs.SetLayerWidth(0, thicknessInFeet);
                    newFloorType.SetCompoundStructure(cs);
                    Debug.WriteLine($"Created single-layer floor type with thickness {thicknessInFeet}ft");
                    return newFloorType;
                }
                else
                {
                    // Find appropriate layer to modify (usually core structural layer)
                    int layerToModify = FindLayerToModify(cs);
                    if (layerToModify < 0)
                        return newFloorType;

                    // Calculate current total thickness
                    double currentTotalThickness = 0;
                    for (int i = 0; i < cs.LayerCount; i++)
                    {
                        currentTotalThickness += cs.GetLayerWidth(i);
                    }

                    // Calculate thickness difference needed
                    double thicknessDiff = thicknessInFeet - currentTotalThickness;

                    // Modify layer width
                    double currentLayerThickness = cs.GetLayerWidth(layerToModify);
                    double newLayerThickness = Math.Max(0.01, currentLayerThickness + thicknessDiff);
                    cs.SetLayerWidth(layerToModify, newLayerThickness);

                    // Apply changes
                    newFloorType.SetCompoundStructure(cs);

                    Debug.WriteLine($"Created multi-layer floor type with adjusted thickness {newLayerThickness}ft");
                    return newFloorType;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating floor type: {ex.Message}");
                return baseFloorType;
            }
        }

        private int FindLayerToModify(DB.CompoundStructure cs)
        {
            // Try to get the core layer first
            int coreLayerIndex = cs.GetFirstCoreLayerIndex();
            if (coreLayerIndex >= 0 && coreLayerIndex < cs.LayerCount)
                return coreLayerIndex;

            // If no core layer, try to find a concrete layer
            for (int i = 0; i < cs.LayerCount; i++)
            {
                DB.Material material = _doc.GetElement(cs.GetMaterialId(i)) as DB.Material;
                if (material != null && material.Name.Contains("Concrete"))
                    return i;
            }

            // If still no match, use the thickest layer
            int thickestLayer = -1;
            double maxThickness = 0;

            for (int i = 0; i < cs.LayerCount; i++)
            {
                double layerThickness = cs.GetLayerWidth(i);
                if (layerThickness > maxThickness)
                {
                    maxThickness = layerThickness;
                    thickestLayer = i;
                }
            }

            return thickestLayer;
        }
    }
}
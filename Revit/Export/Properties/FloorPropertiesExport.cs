using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;
using System.Diagnostics;

namespace Revit.Export.Properties
{
    public class FloorPropertiesExport
    {
        private readonly DB.Document _doc;
        private Dictionary<DB.ElementId, string> _materialIdMap = new Dictionary<DB.ElementId, string>();

        public FloorPropertiesExport(DB.Document doc)
        {
            _doc = doc;
            CreateMaterialIdMapping();
        }

        private void CreateMaterialIdMapping()
        {
            // Map Revit material IDs to model material IDs
            foreach (DB.Material material in new DB.FilteredElementCollector(_doc)
                .OfClass(typeof(DB.Material))
                .Cast<DB.Material>())
            {
                if (!string.IsNullOrEmpty(material.Name))
                {
                    string materialId = $"MAT-{material.Name.Replace(" ", "")}";
                    _materialIdMap[material.Id] = materialId;
                }
            }
        }

        public int Export(List<FloorProperties> floorProperties)
        {
            int count = 0;
            HashSet<DB.ElementId> structuralFloorTypeIds = new HashSet<DB.ElementId>();

            // First, collect floor types that are used by structural floors
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Floor> floors = collector.OfClass(typeof(DB.Floor))
                .WhereElementIsNotElementType()
                .Cast<DB.Floor>()
                .Where(f => IsStructuralFloor(f))
                .ToList();

            foreach (var floor in floors)
            {
                structuralFloorTypeIds.Add(floor.GetTypeId());
            }

            Debug.WriteLine($"Found {structuralFloorTypeIds.Count} floor types used by structural floors");

            // Export only floor types used by structural floors
            foreach (var typeId in structuralFloorTypeIds)
            {
                try
                {
                    DB.FloorType floorType = _doc.GetElement(typeId) as DB.FloorType;
                    if (floorType == null)
                        continue;

                    DB.CompoundStructure cs = floorType.GetCompoundStructure();
                    if (cs == null)
                        continue;

                    // Get material ID
                    string materialId = GetMaterialId(cs);
                    if (string.IsNullOrEmpty(materialId))
                        materialId = "MAT-default";

                    // Determine floor type
                    string typeString = DetermineFloorType(floorType);

                    // Get thickness in inches
                    double thickness = cs.GetWidth() * 12.0;

                    // Create floor property
                    FloorProperties floorProperty = new FloorProperties(
                        floorType.Name,
                        typeString,
                        thickness,
                        materialId
                    );

                    // Add specific properties based on type
                    SetFloorSpecificProperties(floorProperty, floorType, cs);

                    floorProperties.Add(floorProperty);
                    count++;

                    Debug.WriteLine($"Exported floor type: {floorType.Name}, Material: {materialId}, Type: {typeString}, Thickness: {thickness}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting floor type: {ex.Message}");
                }
            }

            return count;
        }

        private bool IsStructuralFloor(DB.Floor floor)
        {
            // Use FLOOR_PARAM_IS_STRUCTURAL parameter as specified
            DB.Parameter isStructuralParam = floor.get_Parameter(DB.BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
            return isStructuralParam != null && isStructuralParam.AsInteger() != 0;
        }

        private string DetermineFloorType(DB.FloorType floorType)
        {
            string typeName = floorType.Name.ToUpper();

            if (typeName.Contains("METAL DECK") || typeName.Contains("DECK") ||
                typeName.Contains("COMPOSITE"))
                return "Composite";
            else
                return "Slab";
        }

        private string GetMaterialId(DB.CompoundStructure cs)
        {
            int coreLayer = cs.GetFirstCoreLayerIndex();
            if (coreLayer >= 0)
            {
                DB.ElementId materialId = cs.GetMaterialId(coreLayer);
                if (materialId != DB.ElementId.InvalidElementId && _materialIdMap.ContainsKey(materialId))
                {
                    return _materialIdMap[materialId];
                }
            }
            return null;
        }

        private void SetFloorSpecificProperties(FloorProperties floorProperty, DB.FloorType floorType, DB.CompoundStructure cs)
        {
            if (floorProperty.Type == "Slab")
            {
                floorProperty.SlabProperties["modelingType"] = "ShellThin";
                floorProperty.SlabProperties["slabType"] = "Slab";
                floorProperty.SlabProperties["isRibbed"] = false;
                floorProperty.SlabProperties["isWaffle"] = false;
                floorProperty.SlabProperties["isTwoWay"] = true;
            }
            else if (floorProperty.Type == "Composite")
            {
                // For composite floors, try to determine deck parameters
                floorProperty.DeckProperties["deckType"] = "Filled";

                // Try to get deck depth from name or parameters
                double deckDepth = 1.5; // Default deck depth in inches
                if (floorType.Name.Contains("1.5"))
                    deckDepth = 1.5;
                else if (floorType.Name.Contains("2"))
                    deckDepth = 2.0;
                else if (floorType.Name.Contains("3"))
                    deckDepth = 3.0;

                floorProperty.DeckProperties["deckDepth"] = deckDepth;
                floorProperty.DeckProperties["deckGage"] = 22; // Default gage
                floorProperty.DeckProperties["deckMaterialName"] = "A992Fy50"; // Default deck material

                // Calculate topping thickness
                double toppingThickness = floorProperty.Thickness - deckDepth;
                if (toppingThickness > 0)
                    floorProperty.DeckProperties["toppingThickness"] = toppingThickness;
            }
        }
    }
}
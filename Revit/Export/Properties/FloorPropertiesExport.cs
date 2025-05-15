using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models;
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
                    StructuralFloorType typeEnum = DetermineFloorType(floorType);

                    // Get thickness in inches
                    double thickness = cs.GetWidth() * 12.0;

                    // Create floor property
                    FloorProperties floorProperty = new FloorProperties(
                        floorType.Name,
                        typeEnum,
                        thickness,
                        materialId
                    );

                    // Set appropriate modeling type
                    floorProperty.ModelingType = ModelingType.Membrane;

                    // For composite floors, set deck properties
                    if (IsDeckType(floorType))
                    {
                        SetDeckProperties(floorProperty, floorType, cs);
                    }

                    floorProperties.Add(floorProperty);
                    count++;

                    Debug.WriteLine($"Exported floor type: {floorType.Name}, Material: {materialId}, Type: {typeEnum}, Thickness: {thickness}");
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

        private StructuralFloorType DetermineFloorType(DB.FloorType floorType)
        {
            string typeName = floorType.Name.ToUpper();

            if (typeName.Contains("METAL DECK") || typeName.Contains("DECK") ||
                typeName.Contains("COMPOSITE"))
                return StructuralFloorType.FilledDeck;
            else if (typeName.Contains("NONCOMPOSITE"))
                return StructuralFloorType.UnfilledDeck;
            else
                return StructuralFloorType.Slab;
        }

        private bool IsDeckType(DB.FloorType floorType)
        {
            string typeName = floorType.Name.ToUpper();
            return typeName.Contains("METAL DECK") || typeName.Contains("DECK") ||
                   typeName.Contains("COMPOSITE") || typeName.Contains("NONCOMPOSITE");
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

        private void SetDeckProperties(FloorProperties floorProperty, DB.FloorType floorType, DB.CompoundStructure cs)
        {
            // For composite/deck floors, set the deck properties
            DeckProperties deckProps = floorProperty.DeckProperties;

            // Try to determine deck type from name
            string typeName = floorType.Name.ToUpper();

            // Set default deck name
            if (typeName.Contains("1.5"))
            {
                deckProps.DeckType = "VULCRAFT 1.5VL";
                deckProps.RibDepth = 1.5;
            }
            else if (typeName.Contains("2"))
            {
                deckProps.DeckType = "VULCRAFT 2VL";
                deckProps.RibDepth = 2.0;
            }
            else if (typeName.Contains("3"))
            {
                deckProps.DeckType = "VULCRAFT 3VL";
                deckProps.RibDepth = 3.0;
            }

            // Set a default steel gage
            if (typeName.Contains("18GA"))
                deckProps.DeckShearThickness = 0.0474;
            else if (typeName.Contains("20GA"))
                deckProps.DeckShearThickness = 0.0358;
            else if (typeName.Contains("22GA"))
                deckProps.DeckShearThickness = 0.0295;

            // Set default deck geometry if not already set
            if (deckProps.RibWidthTop == 0)
                deckProps.RibWidthTop = 6.0;

            if (deckProps.RibWidthBottom == 0)
                deckProps.RibWidthBottom = 4.0;

            if (deckProps.RibSpacing == 0)
                deckProps.RibSpacing = 12.0;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;

namespace Revit.Export.Properties
{
    public class FloorPropertiesExport
    {
        private readonly DB.Document _doc;

        public FloorPropertiesExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<FloorProperties> floorProperties)
        {
            int count = 0;

            // Get all floor types from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FloorType> floorTypes = collector.OfClass(typeof(DB.FloorType))
                .Cast<DB.FloorType>()
                .ToList();

            foreach (var floorType in floorTypes)
            {
                try
                {
                    // Get compound structure for thickness
                    DB.CompoundStructure cs = floorType.GetCompoundStructure();
                    if (cs == null)
                        continue;

                    // Get total thickness in inches
                    double thickness = cs.GetWidth() * 12.0;  // Convert feet to inches

                    // Determine floor type
                    string typeString = "Slab"; // Default
                    if (floorType.Name.Contains("Metal Deck") || floorType.Name.Contains("Deck"))
                    {
                        typeString = "Composite";
                    }

                    // Create FloorProperties
                    FloorProperties floorProperty = new FloorProperties(
                        floorType.Name,
                        typeString,
                        thickness,
                        GetMaterialIdForFloorType(floorType)
                    );

                    // Add additional properties
                    floorProperty.SlabProperties["isRibbed"] = false;
                    floorProperty.SlabProperties["isWaffle"] = false;
                    floorProperty.SlabProperties["isTwoWay"] = true;

                    // Add deck properties if it's a composite floor
                    if (typeString == "Composite")
                    {
                        floorProperty.DeckProperties["deckType"] = "Composite";
                        floorProperty.DeckProperties["deckDepth"] = 1.5; // Default depth in inches
                        floorProperty.DeckProperties["deckGage"] = 22;   // Default gage
                        floorProperty.DeckProperties["toppingThickness"] = thickness - 1.5;
                    }

                    floorProperties.Add(floorProperty);
                    count++;
                }
                catch (Exception)
                {
                    // Skip this floor type and continue with the next one
                }
            }

            return count;
        }

        private string GetMaterialIdForFloorType(DB.FloorType floorType)
        {
            // Try to get the primary material
            try
            {
                DB.CompoundStructure cs = floorType.GetCompoundStructure();
                if (cs != null)
                {
                    int mainLayerIndex = cs.GetFirstCoreLayerIndex();
                    if (mainLayerIndex >= 0 && mainLayerIndex < cs.LayerCount)
                    {
                        DB.ElementId materialId = cs.GetMaterialId(mainLayerIndex);
                        if (materialId != DB.ElementId.InvalidElementId)
                        {
                            DB.Material material = _doc.GetElement(materialId) as DB.Material;
                            if (material != null)
                            {
                                // Create a predictable material ID
                                return $"MAT-{material.Name.Replace(" ", "")}";
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fall back to default material ID
            }

            return "MAT-default";
        }
    }
}
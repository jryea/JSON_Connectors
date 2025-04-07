using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Properties
{
    public class FloorPropertiesExport
    {
        private IModel _model;
        private string _lengthUnit;

        public FloorPropertiesExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public List<FloorProperties> Export()
        {
            var floorProperties = new List<FloorProperties>();

            try
            {
                // Export concrete slabs
                floorProperties.AddRange(ExportConcreteSlabs());

                // Export composite decks
                floorProperties.AddRange(ExportCompositeDecks());

                // Export non-composite decks
                floorProperties.AddRange(ExportNonCompositeDecks());

                return floorProperties;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting floor properties from RAM: {ex.Message}");
                return floorProperties;
            }
        }

        private List<FloorProperties> ExportConcreteSlabs()
        {
            var slabProperties = new List<FloorProperties>();

            try
            {
                // Get concrete slab properties from RAM
                IConcSlabProps concSlabProps = _model.GetConcreteSlabProps();
                if (concSlabProps == null || concSlabProps.GetCount() == 0)
                    return slabProperties;

                // Find concrete material ID
                string concreteMaterialId = FindOrCreateConcreteMaterialId();

                // Process each slab property
                for (int i = 0; i < concSlabProps.GetCount(); i++)
                {
                    IConcSlabProp slabProp = concSlabProps.GetAt(i);
                    if (slabProp == null)
                        continue;

                    FloorProperties floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = slabProp.strLabel,
                        Type = "Slab",
                        Thickness = ConvertFromInches(slabProp.dThickness),
                        MaterialId = concreteMaterialId
                    };

                    // Add slab-specific properties
                    floorProp.SlabProperties["isRibbed"] = false;
                    floorProp.SlabProperties["isWaffle"] = false;
                    floorProp.SlabProperties["isTwoWay"] = true;

                    slabProperties.Add(floorProp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting concrete slab properties: {ex.Message}");
            }

            return slabProperties;
        }

        private List<FloorProperties> ExportCompositeDecks()
        {
            var deckProperties = new List<FloorProperties>();

            try
            {
                // Get composite deck properties from RAM
                ICompDeckProps compDeckProps = _model.GetCompositeDeckProps();
                if (compDeckProps == null || compDeckProps.GetCount() == 0)
                    return deckProperties;

                // Find concrete material ID
                string concreteMaterialId = FindOrCreateConcreteMaterialId();

                // Process each composite deck property
                for (int i = 0; i < compDeckProps.GetCount(); i++)
                {
                    ICompDeckProp deckProp = compDeckProps.GetAt(i);
                    if (deckProp == null)
                        continue;

                    // Calculate total thickness (topping + deck depth)
                    double deckDepth = 1.5; // Default deck depth assumption
                    double totalThickness = ConvertFromInches(deckProp.dToppingThickness + deckDepth);

                    FloorProperties floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = deckProp.strLabel,
                        Type = "Composite",
                        Thickness = totalThickness,
                        MaterialId = concreteMaterialId
                    };

                    // Add deck-specific properties
                    floorProp.DeckProperties["deckType"] = "Composite";
                    floorProp.DeckProperties["deckDepth"] = ConvertFromInches(deckDepth);
                    floorProp.DeckProperties["deckGage"] = 22; // Default gage assumption
                    floorProp.DeckProperties["toppingThickness"] = ConvertFromInches(deckProp.dToppingThickness);

                    deckProperties.Add(floorProp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting composite deck properties: {ex.Message}");
            }

            return deckProperties;
        }

        private List<FloorProperties> ExportNonCompositeDecks()
        {
            var deckProperties = new List<FloorProperties>();

            try
            {
                // Get non-composite deck properties from RAM
                INonCompDeckProps nonCompDeckProps = _model.GetNonCompDeckProps();
                if (nonCompDeckProps == null || nonCompDeckProps.GetCount() == 0)
                    return deckProperties;

                // Find steel material ID
                string steelMaterialId = FindOrCreateSteelMaterialId();

                // Process each non-composite deck property
                for (int i = 0; i < nonCompDeckProps.GetCount(); i++)
                {
                    INonCompDeckProp deckProp = nonCompDeckProps.GetAt(i);
                    if (deckProp == null)
                        continue;

                    FloorProperties floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = deckProp.strLabel,
                        Type = "NonComposite",
                        Thickness = ConvertFromInches(deckProp.dEffectiveThickness),
                        MaterialId = steelMaterialId
                    };

                    // Add deck-specific properties
                    floorProp.DeckProperties["deckType"] = "MetalDeck";
                    floorProp.DeckProperties["deckDepth"] = ConvertFromInches(deckProp.dEffectiveThickness);
                    floorProp.DeckProperties["deckGage"] = 22; // Default gage assumption

                    deckProperties.Add(floorProp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting non-composite deck properties: {ex.Message}");
            }

            return deckProperties;
        }

        private string FindOrCreateConcreteMaterialId()
        {
            // In a real implementation, this would look for an existing concrete material
            // or create a new one if not found
            return IdGenerator.Generate(IdGenerator.Properties.MATERIAL);
        }

        private string FindOrCreateSteelMaterialId()
        {
            // In a real implementation, this would look for an existing steel material
            // or create a new one if not found
            return IdGenerator.Generate(IdGenerator.Properties.MATERIAL);
        }

        private double ConvertFromInches(double inches)
        {
            switch (_lengthUnit.ToLower())
            {
                case "feet":
                    return inches / 12.0;
                case "millimeters":
                    return inches * 25.4;
                case "centimeters":
                    return inches * 2.54;
                case "meters":
                    return inches * 0.0254;
                case "inches":
                default:
                    return inches;
            }
        }
    }
}
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
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public FloorPropertiesExport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        public List<FloorProperties> Export()
        {
            var floorProperties = new List<FloorProperties>();

            try
            {
                // Get concrete material ID
                string concreteMaterialId = _materialProvider.GetConcreteMaterialId();

                // Export concrete slabs
                floorProperties.AddRange(ExportConcreteSlabs(concreteMaterialId));

                // Export composite decks
                floorProperties.AddRange(ExportCompositeDecks(concreteMaterialId));

                // Export non-composite decks
                floorProperties.AddRange(ExportNonCompositeDecks());

                return floorProperties;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting floor properties from RAM: {ex.Message}");

                // Create at least one default property if export fails
                if (floorProperties.Count == 0)
                {
                    floorProperties.Add(CreateDefaultSlabProperty());
                }

                return floorProperties;
            }
        }

        private List<FloorProperties> ExportConcreteSlabs(string materialId)
        {
            var slabProperties = new List<FloorProperties>();

            try
            {
                // Get concrete slab properties from RAM
                IConcSlabProps concSlabProps = _model.GetConcreteSlabProps();
                if (concSlabProps == null || concSlabProps.GetCount() == 0)
                {
                    // Add a default slab property
                    slabProperties.Add(CreateDefaultSlabProperty());
                    return slabProperties;
                }

                // Process each slab property
                for (int i = 0; i < concSlabProps.GetCount(); i++)
                {
                    IConcSlabProp slabProp = concSlabProps.GetAt(i);
                    if (slabProp == null)
                        continue;

                    double thickness = UnitConversionUtils.ConvertFromInches(slabProp.dThickness, _lengthUnit);

                    // Create floor property
                    var floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = slabProp.strLabel ?? $"Slab {thickness:F1}\"",
                        Type = StructuralFloorType.Slab,
                        Thickness = thickness,
                        MaterialId = materialId,
                        ModelingType = ModelingType.Membrane,
                        SlabType = SlabType.Slab
                    };

                    slabProperties.Add(floorProp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting concrete slab properties: {ex.Message}");
                // Add a default slab property
                slabProperties.Add(CreateDefaultSlabProperty());
            }

            return slabProperties;
        }

        private List<FloorProperties> ExportCompositeDecks(string materialId)
        {
            var deckProperties = new List<FloorProperties>();

            try
            {
                // Get composite deck properties from RAM
                ICompDeckProps compDeckProps = _model.GetCompositeDeckProps();
                if (compDeckProps == null || compDeckProps.GetCount() == 0)
                {
                    // Return empty list if no composite decks
                    return deckProperties;
                }

                // Process each composite deck property
                for (int i = 0; i < compDeckProps.GetCount(); i++)
                {
                    ICompDeckProp deckProp = compDeckProps.GetAt(i);
                    if (deckProp == null)
                        continue;

                    // Get deck physical properties
                    double deckDepth = 1.5; // Default deck depth
                    double toppingThickness = UnitConversionUtils.ConvertFromInches(deckProp.dThickAboveFlutes, _lengthUnit);
                    double totalThickness = toppingThickness + deckDepth;

                    // Create floor property
                    var floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = deckProp.strLabel ?? $"Composite Deck {totalThickness:F1}\"",
                        Type = StructuralFloorType.FilledDeck,
                        Thickness = totalThickness,
                        MaterialId = materialId,
                        ModelingType = ModelingType.Membrane,
                        SlabType = SlabType.Slab
                    };

                    // Set deck properties
                    floorProp.DeckProperties.DeckType = deckProp.strDeckType ?? "VULCRAFT 2VL";
                    floorProp.DeckProperties.RibDepth = deckDepth;
                    floorProp.DeckProperties.DeckUnitWeight = deckProp.dSelfWtDeck;

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
                // Get steel material ID for non-composite decks
                string steelMaterialId = _materialProvider.GetSteelMaterialId();

                // Get non-composite deck properties from RAM
                INonCompDeckProps nonCompDeckProps = _model.GetNonCompDeckProps();
                if (nonCompDeckProps == null || nonCompDeckProps.GetCount() == 0)
                {
                    // Return empty list if no non-composite decks
                    return deckProperties;
                }

                // Process each non-composite deck property
                for (int i = 0; i < nonCompDeckProps.GetCount(); i++)
                {
                    INonCompDeckProp deckProp = nonCompDeckProps.GetAt(i);
                    if (deckProp == null)
                        continue;

                    double effectiveThickness = UnitConversionUtils.ConvertFromInches(deckProp.dEffectiveThickness, _lengthUnit);

                    // Create floor property
                    var floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = deckProp.strLabel ?? $"Non-Composite Deck {effectiveThickness:F1}\"",
                        Type = StructuralFloorType.UnfilledDeck,
                        Thickness = effectiveThickness,
                        MaterialId = steelMaterialId,
                        ModelingType = ModelingType.Membrane
                    };

                    // Set deck properties
                    floorProp.DeckProperties.DeckType = "VULCRAFT 1.5VL"; // Default deck type
                    floorProp.DeckProperties.DeckUnitWeight = deckProp.dSelfWeight;

                    deckProperties.Add(floorProp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting non-composite deck properties: {ex.Message}");
            }

            return deckProperties;
        }

        private FloorProperties CreateDefaultSlabProperty()
        {
            return new FloorProperties
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                Name = "Default Concrete Slab",
                Type = StructuralFloorType.Slab,
                Thickness = 6.0, // 6 inch default
                MaterialId = _materialProvider.GetConcreteMaterialId(),
                ModelingType = ModelingType.Membrane,
                SlabType = SlabType.Slab
            };
        }
    }
}
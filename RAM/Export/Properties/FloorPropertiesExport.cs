using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Core.Models;
using Core.Models.Properties;
using Core.Utilities;
using Core.Data;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Properties
{
    public class FloorPropertiesExport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;
        private readonly Dictionary<string, string> _floorPropMappings = new Dictionary<string, string>();

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

                // Store mappings in ModelMappingUtility for FloorExport to use
                ModelMappingUtility.SetFloorPropertiesMappings(_floorPropMappings);

                Console.WriteLine($"Exported {floorProperties.Count} floor properties with {_floorPropMappings.Count} UID mappings");

                return floorProperties;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting floor properties from RAM: {ex.Message}");

                return floorProperties;
            }
        }

        private List<FloorProperties> ExportConcreteSlabs(string materialId)
        {
            var slabProperties = new List<FloorProperties>();

            try
            {
                IConcSlabProps concSlabProps = _model.GetConcreteSlabProps();

                for (int i = 0; i < concSlabProps.GetCount(); i++)
                {
                    IConcSlabProp slabProp = concSlabProps.GetAt(i);
                    if (slabProp == null)
                        continue;

                    double thickness = UnitConversionUtils.ConvertFromInches(slabProp.dThickness, _lengthUnit);

                    // Use FloorPropertyProcessor
                    var floorProp = FloorPropertyProcessor.ProcessConcreteSlabProperties(
                        thickness,
                        materialId,
                        slabProp.strLabel
                    );

                    slabProperties.Add(floorProp);

                    // Store mapping: RAM property UID -> FloorProperties ID
                    _floorPropMappings[slabProp.lUID.ToString()] = floorProp.Id;
                    Console.WriteLine($"Mapped slab UID {slabProp.lUID} -> FloorProperties {floorProp.Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting concrete slab properties: {ex.Message}");
            }

            return slabProperties;
        }

        private List<FloorProperties> ExportCompositeDecks(string materialId)
        {
            var deckProperties = new List<FloorProperties>();

            try
            {
                ICompDeckProps compDeckProps = _model.GetCompositeDeckProps();
                if (compDeckProps == null || compDeckProps.GetCount() == 0)
                {
                    return deckProperties;
                }

                for (int i = 0; i < compDeckProps.GetCount(); i++)
                {
                    ICompDeckProp deckProp = compDeckProps.GetAt(i);
                    if (deckProp == null)
                        continue;

                    // Use StructuralDeck to find deck by type
                    var structuralDeck = StructuralDeck.FindByType(deckProp.strDeckType);

                    FloorProperties floorProp;

                    if (structuralDeck != null)
                    {
                        Console.WriteLine($"  Found deck: {structuralDeck.DeckType}, RibWidthTop={structuralDeck.RibWidthTop}");

                        // Use FloorPropertyProcessor with found deck
                        double concreteThickness = UnitConversionUtils.ConvertFromInches(
                            deckProp.dThickAboveFlutes, _lengthUnit);

                        floorProp = FloorPropertyProcessor.ProcessFromStructuralDeck(
                            structuralDeck,
                            concreteThickness,
                            materialId,
                            StructuralFloorType.FilledDeck,
                            deckProp.strLabel
                        );
                    }
                    else
                    {
                        // Fallback - use ProcessFromDeckType which handles missing decks gracefully
                        double concreteThickness = UnitConversionUtils.ConvertFromInches(
                            deckProp.dThickAboveFlutes, _lengthUnit);

                        floorProp = FloorPropertyProcessor.ProcessFromDeckType(
                            deckProp.strDeckType ?? "VULCRAFT 2VL", // Default fallback
                            concreteThickness,
                            materialId,
                            StructuralFloorType.FilledDeck,
                            deckProp.strLabel
                        );
                    }

                    deckProperties.Add(floorProp);

                    // Store mapping: RAM property UID -> FloorProperties ID
                    _floorPropMappings[deckProp.lUID.ToString()] = floorProp.Id;
                    Console.WriteLine($"Mapped composite deck UID {deckProp.lUID} -> FloorProperties {floorProp.Id}");
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
                string steelMaterialId = _materialProvider.GetSteelMaterialId();
                INonCompDeckProps nonCompDeckProps = _model.GetNonCompDeckProps();
                if (nonCompDeckProps == null || nonCompDeckProps.GetCount() == 0)
                {
                    return deckProperties;
                }

                for (int i = 0; i < nonCompDeckProps.GetCount(); i++)
                {
                    INonCompDeckProp deckProp = nonCompDeckProps.GetAt(i);
                    if (deckProp == null)
                        continue;

                    double effectiveThickness = UnitConversionUtils.ConvertFromInches(
                        deckProp.dEffectiveThickness, _lengthUnit);

                    // Use StructuralDeck to find preferred deck by depth
                    var structuralDeck = StructuralDeck.GetPreferredDeck(effectiveThickness);

                    FloorProperties floorProp;

                    if (structuralDeck != null)
                    {
                        Console.WriteLine($"  Found deck: {structuralDeck.DeckType}, RibWidthTop={structuralDeck.RibWidthTop}");

                        floorProp = FloorPropertyProcessor.ProcessFromStructuralDeck(
                            structuralDeck,
                            0, // No concrete
                            steelMaterialId,
                            StructuralFloorType.UnfilledDeck,
                            deckProp.strLabel
                        );
                    }
                    else
                    {
                        // Fallback - this should rarely happen with the new approach
                        floorProp = new FloorProperties
                        {
                            Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                            Name = deckProp.strLabel ?? $"Non-Composite Deck {effectiveThickness:F1}\"",
                            Type = StructuralFloorType.UnfilledDeck,
                            Thickness = effectiveThickness,
                            MaterialId = steelMaterialId,
                            ModelingType = ModelingType.Membrane
                        };

                        floorProp.DeckProperties = new DeckProperties
                        {
                            DeckType = "VULCRAFT 1.5VL", // Default fallback
                            RibDepth = effectiveThickness,
                            DeckUnitWeight = deckProp.dSelfWeight
                        };
                    }

                    deckProperties.Add(floorProp);

                    // Store mapping: RAM property UID -> FloorProperties ID
                    _floorPropMappings[deckProp.lUID.ToString()] = floorProp.Id;
                    Console.WriteLine($"Mapped non-composite deck UID {deckProp.lUID} -> FloorProperties {floorProp.Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting non-composite deck properties: {ex.Message}");
            }

            return deckProperties;
        }
    }
}
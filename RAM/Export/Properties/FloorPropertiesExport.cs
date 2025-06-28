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
using Core.Processors;

namespace RAM.Export.Properties
{
    public class FloorPropertiesExport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;
        private readonly Dictionary<string, string> _floorPropMappings = new Dictionary<string, string>();
        private List<StructuralDeck> _availableDecks;

        public FloorPropertiesExport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
            LoadDeckData();
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

                // Create at least one default property if export fails
                if (floorProperties.Count == 0)
                {
                    floorProperties.Add(CreateDefaultSlabProperty());
                }

                return floorProperties;
            }
        }

        private void LoadDeckData()
        {
            try
            {
                // Try multiple possible paths
                string[] possiblePaths = new string[]
                {
            Path.Combine("Data", "Tables", "MetalDecks.json"),
            Path.Combine("..", "..", "..", "Data", "Tables", "MetalDecks.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Tables", "MetalDecks.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "Tables", "MetalDecks.json"),
            Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Data", "Tables", "MetalDecks.json")
                };

                string jsonPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        jsonPath = path;
                        break;
                    }
                }

                if (jsonPath != null)
                {
                    string jsonContent = File.ReadAllText(jsonPath);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    };

                    var deckData = JsonSerializer.Deserialize<DeckDataFile>(jsonContent, options);
                    _availableDecks = deckData?.StructuralDecks ?? new List<StructuralDeck>();
                }
                else
                {
                    Console.WriteLine($"MetalDecks.json not found. Searched in:");
                    foreach (var path in possiblePaths)
                    {
                        Console.WriteLine($"  - {Path.GetFullPath(path)}");
                    }
                    _availableDecks = new List<StructuralDeck>();
                }
            }
            catch
            {
                _availableDecks = new List<StructuralDeck>();
            }
        }

        private List<FloorProperties> ExportConcreteSlabs(string materialId)
        {
            var slabProperties = new List<FloorProperties>();

            try
            {
                IConcSlabProps concSlabProps = _model.GetConcreteSlabProps();
                if (concSlabProps == null || concSlabProps.GetCount() == 0)
                {
                    var defaultSlab = CreateDefaultSlabProperty();
                    slabProperties.Add(defaultSlab);
                    return slabProperties;
                }

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
                var defaultSlab = CreateDefaultSlabProperty();
                slabProperties.Add(defaultSlab);
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

                    // Try to find matching deck in MetalDecks.json
                    var structuralDeck = _availableDecks.FirstOrDefault(d =>
                        d.DeckType.Equals(deckProp.strDeckType, StringComparison.OrdinalIgnoreCase));

                    FloorProperties floorProp;

                    Console.WriteLine($"  Found deck: {structuralDeck.DeckType}, RibWidthTop={structuralDeck.RibWidthTop}");

                    if (structuralDeck != null)
                    {
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
                        // Fallback to existing logic
                        string deckType = deckProp.strDeckType ?? "VULCRAFT 2VL";
                        double deckDepth = 1.5; // default
                        if (deckType.Contains("1.5VL"))
                            deckDepth = 1.5;
                        else if (deckType.Contains("2VL"))
                            deckDepth = 2.0;
                        else if (deckType.Contains("3VL"))
                            deckDepth = 3.0;

                        double toppingThickness = UnitConversionUtils.ConvertFromInches(deckProp.dThickAboveFlutes, _lengthUnit);
                        double totalThickness = toppingThickness + deckDepth;

                        floorProp = new FloorProperties
                        {
                            Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                            Name = deckProp.strLabel ?? $"Composite Deck {totalThickness:F1}\"",
                            Type = StructuralFloorType.FilledDeck,
                            Thickness = totalThickness,
                            MaterialId = materialId,
                            ModelingType = ModelingType.Membrane,
                            SlabType = SlabType.Slab
                        };

                        floorProp.DeckProperties.DeckType = deckProp.strDeckType ?? "VULCRAFT 2VL";
                        floorProp.DeckProperties.RibDepth = deckDepth;
                        floorProp.DeckProperties.DeckUnitWeight = deckProp.dSelfWtDeck;
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

                    // Try to match deck by thickness
                    var structuralDeck = _availableDecks
                        .Where(d => Math.Abs(d.RibDepth - effectiveThickness) < 0.1)
                        .FirstOrDefault();

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
                        // Fallback to existing logic
                        floorProp = new FloorProperties
                        {
                            Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                            Name = deckProp.strLabel ?? $"Non-Composite Deck {effectiveThickness:F1}\"",
                            Type = StructuralFloorType.UnfilledDeck,
                            Thickness = effectiveThickness,
                            MaterialId = steelMaterialId,
                            ModelingType = ModelingType.Membrane
                        };

                        floorProp.DeckProperties.DeckType = "VULCRAFT 1.5VL";
                        floorProp.DeckProperties.DeckUnitWeight = deckProp.dSelfWeight;
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

        private FloorProperties CreateDefaultSlabProperty()
        {
            return FloorPropertyProcessor.ProcessConcreteSlabProperties(
                6.0, // 6 inch default
                _materialProvider.GetConcreteMaterialId(),
                "Default Concrete Slab"
            );
        }

        // Add helper class for JSON deserialization
        private class DeckDataFile
        {
            public List<StructuralDeck> StructuralDecks { get; set; }
        }
    }
}
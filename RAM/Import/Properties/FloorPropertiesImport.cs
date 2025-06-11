using System;
using System.Collections.Generic;
using Core.Models;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Properties
{
    // Consolidated importer for all floor property types (slabs, composite decks, non-composite decks)
    public class FloorPropertiesImport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public FloorPropertiesImport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        // Import all floor properties
        public Dictionary<string, int> Import(IEnumerable<FloorProperties> floorProperties)
        {
            var idMapping = new Dictionary<string, int>();

            try
            {
                // Import concrete slabs
                var slabMappings = ImportConcreteSlabs(floorProperties);
                foreach (var mapping in slabMappings)
                {
                    idMapping[mapping.Key] = mapping.Value;
                }

                // Import composite decks
                var compositeMappings = ImportCompositeDecks(floorProperties);
                foreach (var mapping in compositeMappings)
                {
                    idMapping[mapping.Key] = mapping.Value;
                }

                // Import non-composite decks
                var nonCompositeMappings = ImportNonCompositeDecks(floorProperties);
                foreach (var mapping in nonCompositeMappings)
                {
                    idMapping[mapping.Key] = mapping.Value;
                }

                return idMapping;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing floor properties: {ex.Message}");
            }
                return idMapping;
        }

        // Import concrete slab properties
        private Dictionary<string, int> ImportConcreteSlabs(IEnumerable<FloorProperties> floorProperties)
        {
            var idMapping = new Dictionary<string, int>();

            try
            {
                IConcSlabProps slabProps = _model.GetConcreteSlabProps();
                if (slabProps == null)
                {
                    Console.WriteLine("Failed to get concrete slab properties from RAM model");
                    return idMapping;
                }

                foreach (var floorProp in floorProperties)
                {
                    if (floorProp.Type != StructuralFloorType.Slab || idMapping.ContainsKey(floorProp.Id))
                        continue;

                    double thickness = UnitConversionUtils.ConvertToInches(floorProp.Thickness, _lengthUnit);

                    // Calculate self weight based on typical concrete density (150 pcf)
                    double selfWeight = thickness / 12.0 * 150.0;

                    IConcSlabProp slabProp = slabProps.Add(
                        floorProp.Name ?? $"Slab {thickness}\"",
                        thickness,
                        selfWeight);

                    if (slabProp != null)
                    {
                        // Set additional properties

                        idMapping[floorProp.Id] = slabProp.lUID;
                        Console.WriteLine($"Created slab property: {floorProp.Name}, ID: {slabProp.lUID}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing concrete slab properties: {ex.Message}");
            }

            return idMapping;
        }

        // Import composite deck properties
        private Dictionary<string, int> ImportCompositeDecks(IEnumerable<FloorProperties> floorProperties)
        {
            var idMapping = new Dictionary<string, int>();

            try
            {
                ICompDeckProps compDeckProps = _model.GetCompositeDeckProps();
                if (compDeckProps == null)
                {
                    Console.WriteLine("Failed to get composite deck properties from RAM model");
                    return idMapping;
                }

                foreach (var floorProp in floorProperties)
                {
                    if (floorProp.Type != StructuralFloorType.FilledDeck || idMapping.ContainsKey(floorProp.Id))
                        continue;

                    // Extract deck properties
                    string deckType = floorProp.DeckProperties?.DeckType ?? "VULCRAFT 1.5VL";
                    double deckDepth = floorProp.DeckProperties?.RibDepth ?? 1.5;

                    // Calculate topping thickness
                    double toppingThickness = floorProp.Thickness - deckDepth;
                    if (toppingThickness <= 0) toppingThickness = 2.5; // Fallback value

                    // Convert to inches
                    toppingThickness = UnitConversionUtils.ConvertToInches(toppingThickness, _lengthUnit);

                    // Default stud properties
                    double studLength = 4.0; // Default stud length in inches

                    try
                    {
                        // Create the composite deck property in RAM
                        ICompDeckProp compDeckProp = compDeckProps.Add2(
                            floorProp.Name ?? $"CompDeck {toppingThickness}\"",
                            "Vulcraft 2VL" ,
                            toppingThickness,
                            studLength);

                        if (compDeckProp != null)
                        {
                            // Set additional properties
                            double selfWeight = floorProp.DeckProperties?.DeckUnitWeight ?? 2.0;
                            compDeckProp.dSelfWtDeck = selfWeight;

                            // Store the mapping
                            idMapping[floorProp.Id] = compDeckProp.lUID;
                            Console.WriteLine($"Created composite deck property: {floorProp.Name}, ID: {compDeckProp.lUID}");
                        }
                        else
                        {
                            Console.WriteLine(
                                $"Failed to create composite deck property for '{floorProp.Name ?? floorProp.Id}'. " +
                                $"Parameters: deckType='{deckType}', toppingThickness={toppingThickness}, studLength={studLength}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Exception while creating composite deck property for '{floorProp.Name ?? floorProp.Id}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing composite deck properties: {ex.Message}");
            }

            return idMapping;
        }

        // Import non-composite deck properties
        private Dictionary<string, int> ImportNonCompositeDecks(IEnumerable<FloorProperties> floorProperties)
        {
            var idMapping = new Dictionary<string, int>();

            try
            {
                INonCompDeckProps nonCompDeckProps = _model.GetNonCompDeckProps();
                if (nonCompDeckProps == null)
                {
                    Console.WriteLine("Failed to get non-composite deck properties from RAM model");
                    return idMapping;
                }

                foreach (var floorProp in floorProperties)
                {
                    if (floorProp.Type != StructuralFloorType.UnfilledDeck || idMapping.ContainsKey(floorProp.Id))
                        continue;

                    // Create the non-composite deck property in RAM
                    INonCompDeckProp nonCompDeckProp = nonCompDeckProps.Add(
                        floorProp.Name ?? $"NonCompDeck {floorProp.Thickness}\"");

                    if (nonCompDeckProp != null)
                    {
                        // Set properties
                        double effectiveThickness = UnitConversionUtils.ConvertToInches(floorProp.Thickness, _lengthUnit);
                        nonCompDeckProp.dEffectiveThickness = effectiveThickness;

                        // Set self weight
                        double selfWeight = floorProp.DeckProperties?.DeckUnitWeight ?? 2.0;
                        nonCompDeckProp.dSelfWeight = selfWeight;

                        // Default steel properties
                        nonCompDeckProp.dElasticModulus = 29000.0; // Steel elastic modulus (ksi)
                        nonCompDeckProp.dPoissonsRatio = 0.3; // Steel Poisson's ratio

                        // Store the mapping
                        idMapping[floorProp.Id] = nonCompDeckProp.lUID;
                        Console.WriteLine($"Created non-composite deck property: {floorProp.Name}, ID: {nonCompDeckProp.lUID}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing non-composite deck properties: {ex.Message}");
            }

            return idMapping;
        }
    }
}
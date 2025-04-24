// NonCompositeDeckPropertiesImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Properties
{
    /// <summary>
    /// Imports non-composite deck properties to RAM from the Core model
    /// </summary>
    public class NonCompositeDeckPropertiesImport
    {
        private IModel _model;
        private string _lengthUnit;

        // Initializes a new instance of the NonCompositeDeckPropertiesImport class
        public NonCompositeDeckPropertiesImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        // Imports non-composite deck properties to RAM
        public Dictionary<string, int> Import(IEnumerable<FloorProperties> floorProperties)
        {
            var idMapping = new Dictionary<string, int>();

            try
            {
                // Get the non-composite deck properties collection from RAM
                INonCompDeckProps nonCompDeckProps = _model.GetNonCompDeckProps();

                foreach (var floorProp in floorProperties)
                {
                    // Skip if not a non-composite deck type or already imported
                    if (floorProp.Type?.ToLower() != "noncomposite" || idMapping.ContainsKey(floorProp.Id))
                        continue;

                    // Create the non-composite deck property in RAM
                    INonCompDeckProp nonCompDeckProp = nonCompDeckProps.Add(
                        floorProp.Name ?? $"NonCompDeck {floorProp.Thickness}\"");

                    if (nonCompDeckProp != null)
                    {
                        // Set additional properties if available
                        double effectiveThickness = UnitConversionUtils.ConvertToInches(floorProp.Thickness, _lengthUnit);
                        double selfWeight = 0.0;

                        if (floorProp.DeckProperties != null)
                        {
                            // Get deck type and gage to calculate self weight
                            string deckType = "VULCRAFT 1.5VL"; // Default
                            int deckGage = 22; // Default

                            if (floorProp.DeckProperties.ContainsKey("deckType") &&
                                floorProp.DeckProperties["deckType"] is string type)
                            {
                                deckType = type;
                            }

                            if (floorProp.DeckProperties.ContainsKey("deckGage") &&
                                floorProp.DeckProperties["deckGage"] is int gage)
                            {
                                deckGage = gage;
                            }

                            // Calculate self weight
                            RAMHelpers.GetDeckProperties(deckType, deckGage, out selfWeight);

                            // Set effective thickness if available
                            if (floorProp.DeckProperties.ContainsKey("effectiveThickness") &&
                                floorProp.DeckProperties["effectiveThickness"] is double thickness)
                            {
                                effectiveThickness = UnitConversionUtils.ConvertToInches(thickness, _lengthUnit);
                            }
                        }

                        // Set properties in RAM
                        nonCompDeckProp.dEffectiveThickness = effectiveThickness;
                        nonCompDeckProp.dSelfWeight = selfWeight;

                        // Optional properties with reasonable defaults
                        nonCompDeckProp.dElasticModulus = 29000.0; // Steel elastic modulus (ksi)
                        nonCompDeckProp.dPoissonsRatio = 0.3; // Steel Poisson's ratio

                        // Store the mapping between Core model ID and RAM ID
                        idMapping[floorProp.Id] = nonCompDeckProp.lUID;
                        Console.WriteLine($"Created non-composite deck property: {floorProp.Name}, ID: {nonCompDeckProp.lUID}");
                    }
                }

                return idMapping;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing non-composite deck properties: {ex.Message}");
                return idMapping;
            }
        }
    }
}
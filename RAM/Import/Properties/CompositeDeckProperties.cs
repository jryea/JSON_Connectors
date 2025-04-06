// CompositeDeckPropertiesImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Properties
{
    // Imports composite deck properties to RAM from the Core model
    public class CompositeDeckPropertiesImport
    {
        private IModel _model;
        private string _lengthUnit;

        // Initializes a new instance of the CompositeDeckPropertiesImport class
     
        public CompositeDeckPropertiesImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        // Imports composite deck properties to RAM
  
        public Dictionary<string, int> Import(IEnumerable<FloorProperties> floorProperties)
        {
            var idMapping = new Dictionary<string, int>();

            try
            {
                // Get the composite deck properties collection from RAM
                ICompDeckProps compDeckProps = _model.GetCompositeDeckProps();

                foreach (var floorProp in floorProperties)
                {
                    // Skip if not a composite deck type or already imported
                    if (floorProp.Type?.ToLower() != "composite" || idMapping.ContainsKey(floorProp.Id))
                        continue;

                    // Get the deck properties
                    string deckType = "VULCRAFT 1.5VL"; // Default deck type
                    int deckGage = 22; // Default deck gage
                    double studLength = 4.0; // Default stud length in inches

                    // Get deck properties from the model if available
                    if (floorProp.DeckProperties != null)
                    {
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

                        if (floorProp.DeckProperties.ContainsKey("studLength") &&
                            floorProp.DeckProperties["studLength"] is double length)
                        {
                            studLength = Helpers.ConvertToInches(length, _lengthUnit);
                        }
                    }

                    // Calculate topping thickness
                    double toppingThickness = 0.0;
                    if (floorProp.DeckProperties != null &&
                        floorProp.DeckProperties.ContainsKey("toppingThickness") &&
                        floorProp.DeckProperties["toppingThickness"] is double topThickness)
                    {
                        toppingThickness = Helpers.ConvertToInches(topThickness, _lengthUnit);
                    }
                    else
                    {
                        // Default: total thickness minus deck depth
                        double deckDepth = floorProp.DeckProperties != null &&
                                          floorProp.DeckProperties.ContainsKey("deckDepth") &&
                                          floorProp.DeckProperties["deckDepth"] is double depth
                                          ? depth : 1.5;

                        toppingThickness = Helpers.ConvertToInches(floorProp.Thickness, _lengthUnit) - deckDepth;
                        if (toppingThickness < 0) toppingThickness = 2.5; // Fallback to a reasonable value
                    }

                    // Get deck self weight based on deck type and gage
                    double selfWeight;
                    Helpers.GetDeckProperties(deckType, deckGage, out selfWeight);

                    // Create the composite deck property in RAM
                    ICompDeckProp compDeckProp = compDeckProps.Add2(
                        floorProp.Name ?? $"CompDeck {toppingThickness}\"",
                        deckType,
                        toppingThickness,
                        studLength);

                    if (compDeckProp != null)
                    {
                        // Set additional properties
                        compDeckProp.dSelfWtDeck = selfWeight;

                        // Store the mapping between Core model ID and RAM ID
                        idMapping[floorProp.Id] = compDeckProp.lUID;
                        Console.WriteLine($"Created composite deck property: {floorProp.Name}, ID: {compDeckProp.lUID}");
                    }
                }

                return idMapping;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing composite deck properties: {ex.Message}");
                return idMapping;
            }
        }
    }
}
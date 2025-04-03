// CompositeDeckPropertiesExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Properties;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class RAMToCompositeDeckProperties : IRAMExporter
    {
        private IModel _model;

        public RAMToCompositeDeckProperties(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Get composite deck properties from RAM model
            ICompDeckProps compDeckProps = _model.GetCompositeDeckProps();

            // Filter for composite deck properties
            var compositeDeckProps = model.Properties.FloorProperties
                .Where(fp => fp.Type?.ToLower() == "composite")
                .ToList();

            // Export each composite deck property
            foreach (var floorProp in compositeDeckProps)
            {
                try
                {
                    // Extract deck specific properties
                    string deckType = "VULCRAFT 1.5VL"; // Default deck type
                    double toppingThickness = floorProp.Thickness;
                    int deckGage = 22; // Default gage
                    double studLength = 4.0; // Default stud length (inches)
                    double selfWeight;

                    // Override defaults with specified values if available
                    if (floorProp.DeckProperties != null)
                    {
                        if (floorProp.DeckProperties.TryGetValue("deckType", out object dtValue) && dtValue is string dt)
                        {
                            deckType = dt;
                        }

                        if (floorProp.DeckProperties.TryGetValue("toppingThickness", out object ttValue) && ttValue is double tt)
                        {
                            toppingThickness = tt;
                        }

                        if (floorProp.DeckProperties.TryGetValue("deckGage", out object dgValue))
                        {
                            if (dgValue is int dg)
                            {
                                deckGage = dg;
                            }
                            else if (dgValue is double dgd)
                            {
                                deckGage = (int)dgd;
                            }
                        }

                        if (floorProp.DeckProperties.TryGetValue("studLength", out object slValue) && slValue is double sl)
                        {
                            studLength = sl;
                        }
                    }

                    // Get deck properties based on type and gage
                    RAM.Utilities.RAMModelConverter.GetDeckProperties(deckType, deckGage, out selfWeight);

                    // Create the composite deck property in RAM
                    ICompDeckProp compDeckProp = compDeckProps.Add2(floorProp.Name, deckType, toppingThickness, studLength);

                    // Set self-weight of deck
                    compDeckProp.dSelfWtDeck = selfWeight;

                    // Set additional properties if available
                    if (floorProp.DeckProperties != null)
                    {
                        // Set whether the deck is shored during construction
                        if (floorProp.DeckProperties.TryGetValue("isShored", out object isValue) && isValue is bool isShored)
                        {
                            compDeckProp.bIsShored = isShored;
                        }

                        // Set stud diameter
                        if (floorProp.DeckProperties.TryGetValue("studDiameter", out object sdValue) && sdValue is double studDiameter)
                        {
                            compDeckProp.dStudDiameter = studDiameter;
                        }

                        // Set stud tensile strength
                        if (floorProp.DeckProperties.TryGetValue("studFu", out object sfValue) && sfValue is double studFu)
                        {
                            compDeckProp.dStudFu = studFu;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting composite deck property {floorProp.Name}: {ex.Message}");
                }
            }
        }
    }
}
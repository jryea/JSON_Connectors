// CompositeDeckPropertiesImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class CompositeDeckPropertiesImporter : IRAMImporter<List<FloorProperties>>
    {
        private IModel _model;
        private Dictionary<int, string> _materialIdMap;

        public CompositeDeckPropertiesImporter(IModel model, Dictionary<int, string> materialIdMap)
        {
            _model = model;
            _materialIdMap = materialIdMap;
        }

        public List<FloorProperties> Import()
        {
            var floorProperties = new List<FloorProperties>();

            try
            {
                // Get composite deck properties from RAM
                ICompDeckProps compDeckProps = _model.GetCompositeDeckProps();

                for (int i = 0; i < compDeckProps.GetCount(); i++)
                {
                    ICompDeckProp compDeckProp = compDeckProps.GetAt(i);

                    // Get material ID for concrete
                    string materialId = null;
                    if (_materialIdMap.ContainsKey(compDeckProp.lConcMaterialId))
                    {
                        materialId = _materialIdMap[compDeckProp.lConcMaterialId];
                    }

                    // Calculate total thickness (topping + deck depth)
                    double toppingThickness = compDeckProp.dSlabThickness;
                    double deckDepth = GetDeckDepthFromType(compDeckProp.strDeckType);
                    double totalThickness = toppingThickness + deckDepth;

                    // Create floor property
                    var floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = compDeckProp.strLabel,
                        Type = "Composite",
                        Thickness = totalThickness,
                        MaterialId = materialId
                    };

                    // Add deck-specific properties
                    floorProp.DeckProperties["deckType"] = compDeckProp.strDeckType;
                    floorProp.DeckProperties["toppingThickness"] = toppingThickness;
                    floorProp.DeckProperties["deckDepth"] = deckDepth;
                    floorProp.DeckProperties["deckGage"] = EstimateDeckGage(compDeckProp.dSelfWtDeck);
                    floorProp.DeckProperties["isShored"] = compDeckProp.bIsShored;
                    floorProp.DeckProperties["studDiameter"] = compDeckProp.dStudDiameter;
                    floorProp.DeckProperties["studLength"] = compDeckProp.dStudLen;

                    floorProperties.Add(floorProp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing composite deck properties: {ex.Message}");
            }

            return floorProperties;
        }

        private double GetDeckDepthFromType(string deckType)
        {
            // Extract deck depth from type name (e.g., "VULCRAFT 1.5VL" => 1.5)
            if (deckType.Contains("1.5"))
                return 1.5;
            else if (deckType.Contains("2"))
                return 2.0;
            else if (deckType.Contains("3"))
                return 3.0;
            else
                return 1.5; // Default
        }

        private int EstimateDeckGage(double selfWeight)
        {
            // Estimate deck gage based on self-weight
            if (selfWeight <= 1.5)
                return 22;
            else if (selfWeight <= 1.8)
                return 20;
            else if (selfWeight <= 2.1)
                return 19;
            else if (selfWeight <= 2.5)
                return 18;
            else
                return 16;
        }
    }
}
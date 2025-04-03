// NonCompositeDeckPropertiesImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class NonCompositeDeckPropertiesImporter : IRAMImporter<List<FloorProperties>>
    {
        private IModel _model;
        private Dictionary<int, string> _materialIdMap;

        public NonCompositeDeckPropertiesImporter(IModel model, Dictionary<int, string> materialIdMap)
        {
            _model = model;
            _materialIdMap = materialIdMap;
        }

        public List<FloorProperties> Import()
        {
            var floorProperties = new List<FloorProperties>();

            try
            {
                // Get non-composite deck properties from RAM
                INonCompDeckProps nonCompDeckProps = _model.GetNonCompDeckProps();

                for (int i = 0; i < nonCompDeckProps.GetCount(); i++)
                {
                    INonCompDeckProp nonCompDeckProp = nonCompDeckProps.GetAt(i);

                    // Find steel material ID (use first available steel material if not matched)
                    string materialId = GetFirstSteelMaterialId();

                    // Create floor property
                    var floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = nonCompDeckProp.strLabel,
                        Type = "NonComposite",
                        Thickness = nonCompDeckProp.dThickness,
                        MaterialId = materialId
                    };

                    // Add deck-specific properties
                    floorProp.DeckProperties["deckType"] = "MetalDeck";
                    floorProp.DeckProperties["deckDepth"] = nonCompDeckProp.dThickness; // Use thickness as deck depth
                    floorProp.DeckProperties["elasticModulus"] = nonCompDeckProp.dElasticMod;
                    floorProp.DeckProperties["poissonsRatio"] = nonCompDeckProp.dPoissonsRatio;
                    floorProp.DeckProperties["selfWeight"] = nonCompDeckProp.dSelfWeight;
                    floorProp.DeckProperties["deckGage"] = EstimateDeckGage(nonCompDeckProp.dSelfWeight);

                    floorProperties.Add(floorProp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing non-composite deck properties: {ex.Message}");
            }

            return floorProperties;
        }

        private string GetFirstSteelMaterialId()
        {
            // Find first available steel material
            ISteelMaterials steelMaterials = _model.GetSteelMaterials();
            for (int i = 0; i < steelMaterials.GetCount(); i++)
            {
                ISteelMaterial steelMaterial = steelMaterials.GetAt(i);
                if (_materialIdMap.ContainsKey(steelMaterial.lUID))
                {
                    return _materialIdMap[steelMaterial.lUID];
                }
            }

            // If no steel material found, return null
            return null;
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
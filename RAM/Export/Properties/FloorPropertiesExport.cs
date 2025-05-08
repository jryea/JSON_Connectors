using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Models.Properties.Floors;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Properties
{
    public class FloorPropertiesExport
    {
        private IModel _model;
        private string _lengthUnit;
        private RAMExporter _exporter;

        public FloorPropertiesExport(IModel model, RAMExporter exporter, string lengthUnit = "inches")
        {
            _model = model;
            _exporter = exporter;
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

                // Process each slab property
                for (int i = 0; i < concSlabProps.GetCount(); i++)
                {
                    IConcSlabProp slabProp = concSlabProps.GetAt(i);
                    if (slabProp == null)
                        continue;

                    double thickness = ConvertFromInches(slabProp.dThickness);
                    bool useElasticModulus = slabProp.bUseElasticModulus;
                    double elasticModulus = slabProp.bUseElasticModulus ?
                        slabProp.dElasticModulus :
                        CalculateElasticModulus(slabProp.dFpc);

                    // Get concrete material ID - using a dummy material ID (1) since slabs don't have direct material ID
                    string concreteMaterialId = _exporter.GetOrCreateMaterialId(1, EMATERIALTYPES.EConcreteMat, _model);

                    FloorProperties floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = slabProp.strLabel,
                        Type = "Slab",
                        Thickness = thickness,
                        MaterialId = concreteMaterialId
                    };

                    // Initialize slab-specific properties
                    floorProp.SlabProps = new SlabProperties
                    {
                        IsRibbed = false,
                        IsWaffle = false,
                        IsTwoWay = true,
                        Reinforcement = "Default"
                    };

                    // Add additional properties to SlabProps.SoftwareSpecificProperties
                    floorProp.SlabProps.SoftwareSpecificProperties["bendingCrackedFactor"] = slabProp.dBendingCrackedFactor;
                    floorProp.SlabProps.SoftwareSpecificProperties["diaphragmCrackedFactor"] = slabProp.dDiaphragmCrackedFactor;
                    floorProp.SlabProps.SoftwareSpecificProperties["elasticModulus"] = elasticModulus;
                    floorProp.SlabProps.SoftwareSpecificProperties["concreteStrength"] = slabProp.dFpc;
                    floorProp.SlabProps.SoftwareSpecificProperties["poissonsRatio"] = slabProp.dPoissonsRatio;
                    floorProp.SlabProps.SoftwareSpecificProperties["selfWeight"] = slabProp.dSelfWeight;
                    floorProp.SlabProps.SoftwareSpecificProperties["unitWeight"] = slabProp.dUnitWeight;
                    floorProp.SlabProps.SoftwareSpecificProperties["useElasticModulus"] = slabProp.bUseElasticModulus;

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

                // Process each composite deck property
                for (int i = 0; i < compDeckProps.GetCount(); i++)
                {
                    ICompDeckProp deckProp = compDeckProps.GetAt(i);
                    if (deckProp == null)
                        continue;

                    // Get deck physical properties
                    double deckDepth = 1.5; // Default deck depth assumption if not available
                    double toppingThickness = ConvertFromInches(deckProp.dToppingThickness);
                    double thicknessAboveFlutes = ConvertFromInches(deckProp.dThickAboveFlutes);
                    double totalThickness = toppingThickness + deckDepth;
                    bool isShored = deckProp.bShored;

                    // Get concrete material ID for topping
                    string concreteMaterialId = _exporter.GetOrCreateMaterialId(1, EMATERIALTYPES.EConcreteMat, _model);

                    FloorProperties floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = deckProp.strLabel,
                        Type = "Composite",
                        Thickness = totalThickness,
                        MaterialId = concreteMaterialId
                    };

                    // Initialize deck-specific properties
                    floorProp.DeckProps = new DeckProperties
                    {
                        DeckType = "Composite",
                        DeckDepth = ConvertFromInches(deckDepth),
                        DeckGage = 22, // Default gage assumption
                        ToppingThickness = toppingThickness,
                        Manufacturer = "Vulcraft", // Default manufacturer
                        ProfileName = deckProp.strDeckType ?? "1.5B" // Use deck type from RAM or default
                    };

                    // Add additional properties from RAM to DeckProps SoftwareSpecificProperties
                    floorProp.DeckProps.SoftwareSpecificProperties["isShored"] = isShored;
                    floorProp.DeckProps.SoftwareSpecificProperties["thicknessAboveFlutes"] = thicknessAboveFlutes;
                    floorProp.DeckProps.SoftwareSpecificProperties["unitWeight"] = deckProp.dUnitWeight;
                    floorProp.DeckProps.SoftwareSpecificProperties["selfWeight"] = deckProp.dSelfWtDeck;
                    floorProp.DeckProps.SoftwareSpecificProperties["elasticModulus"] = deckProp.dElasticModulus;
                    floorProp.DeckProps.SoftwareSpecificProperties["poissonsRatio"] = deckProp.dPoissonsRatio;
                    floorProp.DeckProps.SoftwareSpecificProperties["concreteCompression"] = deckProp.dFpc;

                    // Add stud connector properties if relevant
                    if (deckProp.dStudLength > 0)
                    {
                        floorProp.DeckProps.SoftwareSpecificProperties["studLength"] = deckProp.dStudLength;
                        floorProp.DeckProps.SoftwareSpecificProperties["studDiameter"] = deckProp.dStudDiameter;
                        floorProp.DeckProps.SoftwareSpecificProperties["studUltimateStrength"] = deckProp.dStudFu;
                    }

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

                // Process each non-composite deck property
                for (int i = 0; i < nonCompDeckProps.GetCount(); i++)
                {
                    INonCompDeckProp deckProp = nonCompDeckProps.GetAt(i);
                    if (deckProp == null)
                        continue;

                    double effectiveThickness = ConvertFromInches(deckProp.dEffectiveThickness);

                    // Get steel material ID
                    string steelMaterialId = _exporter.GetOrCreateMaterialId(1, EMATERIALTYPES.ESteelMat, _model);

                    FloorProperties floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = deckProp.strLabel,
                        Type = "NonComposite",
                        Thickness = effectiveThickness,
                        MaterialId = steelMaterialId
                    };

                    // Initialize deck-specific properties
                    floorProp.DeckProps = new DeckProperties
                    {
                        DeckType = "NonComposite",
                        DeckDepth = effectiveThickness,
                        DeckGage = 22, // Default gage assumption
                        Manufacturer = "Vulcraft", // Default manufacturer
                        ProfileName = "1.5B"       // Default profile
                    };

                    // Add additional properties from RAM to DeckProps SoftwareSpecificProperties
                    floorProp.DeckProps.SoftwareSpecificProperties["effectiveThickness"] = effectiveThickness;
                    floorProp.DeckProps.SoftwareSpecificProperties["selfWeight"] = deckProp.dSelfWeight;
                    floorProp.DeckProps.SoftwareSpecificProperties["elasticModulus"] = deckProp.dElasticModulus;
                    floorProp.DeckProps.SoftwareSpecificProperties["poissonsRatio"] = deckProp.dPoissonsRatio;

                    // If the deck section type/gauge is encoded in the label, try to parse it
                    string label = deckProp.strLabel;
                    if (!string.IsNullOrEmpty(label))
                    {
                        // Try to extract deck type and gage from the name (e.g., "1.5VL 22GA")
                        ParseDeckTypeAndGage(label, floorProp);
                    }

                    deckProperties.Add(floorProp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting non-composite deck properties: {ex.Message}");
            }

            return deckProperties;
        }

        // Helper method to parse deck type and gage from label
        private void ParseDeckTypeAndGage(string label, FloorProperties floorProp)
        {
            try
            {
                if (label.Contains("VL") || label.Contains("B") || label.Contains("N") || label.Contains("F"))
                {
                    // Try to extract deck type
                    if (label.Contains("1.5") && label.Contains("VL"))
                        floorProp.DeckProps.ProfileName = "1.5VL";
                    else if (label.Contains("2") && label.Contains("VL"))
                        floorProp.DeckProps.ProfileName = "2VL";
                    else if (label.Contains("3") && label.Contains("VL"))
                        floorProp.DeckProps.ProfileName = "3VL";
                    else if (label.Contains("1.5") && label.Contains("B"))
                        floorProp.DeckProps.ProfileName = "1.5B";
                    else if (label.Contains("3") && label.Contains("N"))
                        floorProp.DeckProps.ProfileName = "3N";
                    else if (label.Contains("3") && label.Contains("DR"))
                        floorProp.DeckProps.ProfileName = "3DR";
                }

                // Try to extract gage
                if (label.Contains("GA"))
                {
                    string[] parts = label.Split(new[] { "GA" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        string gagePart = parts[0].Trim();
                        // Get last word before GA
                        string[] words = gagePart.Split(' ');
                        string lastWord = words[words.Length - 1];

                        if (int.TryParse(lastWord, out int gage))
                        {
                            floorProp.DeckProps.DeckGage = gage;
                        }
                    }
                }
                else if (label.Contains("22") || label.Contains("20") || label.Contains("18") || label.Contains("16"))
                {
                    // Look for common gage values
                    if (label.Contains("22"))
                        floorProp.DeckProps.DeckGage = 22;
                    else if (label.Contains("20"))
                        floorProp.DeckProps.DeckGage = 20;
                    else if (label.Contains("18"))
                        floorProp.DeckProps.DeckGage = 18;
                    else if (label.Contains("16"))
                        floorProp.DeckProps.DeckGage = 16;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing deck type and gage from label '{label}': {ex.Message}");
            }
        }

        // Calculate elastic modulus from concrete strength (ACI formula)
        private double CalculateElasticModulus(double fpc)
        {
            return 57000.0 * Math.Sqrt(fpc);
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
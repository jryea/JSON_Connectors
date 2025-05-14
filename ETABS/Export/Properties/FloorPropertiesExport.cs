using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models;
using Core.Models.Properties;
using Core.Utilities;

namespace ETABS.Export.Properties
{
    // Imports floor property definitions from ETABS E2K file
    public class FloorPropertiesExport
    {
        // Dictionary to map material names to IDs
        private Dictionary<string, string> _materialIdsByName = new Dictionary<string, string>();

        // Sets the material name to ID mapping for reference when creating floor properties
        public void SetMaterials(IEnumerable<Material> materials)
        {
            _materialIdsByName.Clear();
            foreach (var material in materials)
            {
                if (!string.IsNullOrEmpty(material.Name))
                {
                    _materialIdsByName[material.Name] = material.Id;
                }
            }
        }

        // Imports floor properties from E2K SLAB PROPERTIES and DECK PROPERTIES sections
        public List<FloorProperties> Export(string slabPropertiesSection, string deckPropertiesSection)
        {
            var floorProperties = new Dictionary<string, FloorProperties>();

            // Import slab properties
            ImportSlabProperties(slabPropertiesSection, floorProperties);

            // Import deck properties
            ImportDeckProperties(deckPropertiesSection, floorProperties);

            return new List<FloorProperties>(floorProperties.Values);
        }

        // Imports slab properties from the SLAB PROPERTIES section
        private void ImportSlabProperties(string slabPropertiesSection, Dictionary<string, FloorProperties> floorProperties)
        {
            if (string.IsNullOrWhiteSpace(slabPropertiesSection))
                return;

            // Regular expression to match slab property definition
            // Format: SHELLPROP "name" PROPTYPE "Slab" MATERIAL "material" MODELINGTYPE "ShellThin" SLABTYPE "type" SLABTHICKNESS thickness
            var slabPattern = new Regex(@"^\s*SHELLPROP\s+""([^""]+)""\s+PROPTYPE\s+""Slab""\s+MATERIAL\s+""([^""]+)""\s+MODELINGTYPE\s+""([^""]+)""\s+SLABTYPE\s+""([^""]+)""\s+SLABTHICKNESS\s+([\d\.]+)",
                RegexOptions.Multiline);

            var slabMatches = slabPattern.Matches(slabPropertiesSection);
            foreach (Match match in slabMatches)
            {
                if (match.Groups.Count >= 6)
                {
                    string name = match.Groups[1].Value;
                    string materialName = match.Groups[2].Value;
                    string modelingTypeStr = match.Groups[3].Value;
                    string slabTypeStr = match.Groups[4].Value;
                    double thickness = Convert.ToDouble(match.Groups[5].Value);

                    // Look up material ID
                    string materialId = null;
                    if (_materialIdsByName.TryGetValue(materialName, out string id))
                    {
                        materialId = id;
                    }

                    // Create floor properties
                    var floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = name,
                        Type = StructuralFloorType.Slab,
                        Thickness = thickness,
                        MaterialId = materialId,
                        ModelingType = ParseModelingType(modelingTypeStr),
                        SlabType = ParseSlabType(slabTypeStr)
                    };

                    floorProperties[name] = floorProp;
                }
            }
        }

        // Imports deck properties from the DECK PROPERTIES section
        private void ImportDeckProperties(string deckPropertiesSection, Dictionary<string, FloorProperties> floorProperties)
        {
            if (string.IsNullOrWhiteSpace(deckPropertiesSection))
                return;

            // Regular expression to match deck property definition
            var deckPattern = new Regex(@"^\s*SHELLPROP\s+""([^""]+)""\s+PROPTYPE\s+""Deck""\s+DECKTYPE\s+""([^""]+)""\s+CONCMATERIAL\s+""([^""]+)""\s+DECKMATERIAL\s+""([^""]+)""\s+DECKSLABDEPTH\s+([\d\.]+)",
                RegexOptions.Multiline);

            var deckMatches = deckPattern.Matches(deckPropertiesSection);
            foreach (Match match in deckMatches)
            {
                if (match.Groups.Count >= 6)
                {
                    string name = match.Groups[1].Value;
                    string deckType = match.Groups[2].Value;
                    string concMaterialName = match.Groups[3].Value;
                    string deckMaterialName = match.Groups[4].Value;
                    double slabDepth = Convert.ToDouble(match.Groups[5].Value);

                    // Look up material ID (use concrete material as primary)
                    string materialId = null;
                    if (_materialIdsByName.TryGetValue(concMaterialName, out string id))
                    {
                        materialId = id;
                    }

                    // Determine floor type based on deck type
                    StructuralFloorType floorType = deckType.ToLower() == "filled" ?
                        StructuralFloorType.FilledDeck : StructuralFloorType.UnfilledDeck;

                    // Create floor properties
                    var floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = name,
                        Type = floorType,
                        Thickness = slabDepth, // Use slab depth as total thickness
                        MaterialId = materialId,
                        // Default to membrane modeling type for deck
                        ModelingType = ModelingType.Membrane,
                        // Default to slab for slabType
                        SlabType = SlabType.Slab
                    };

                    // Initialize DeckProperties
                    floorProp.DeckProperties = new DeckProperties
                    {
                        DeckType = deckType
                    };

                    // Extract additional deck properties if available in the match line
                    string fullLine = match.Value;

                    // Rib depth
                    var ribDepthMatch = Regex.Match(fullLine, @"DECKRIBDEPTH\s+([\d\.]+)");
                    if (ribDepthMatch.Success)
                    {
                        floorProp.DeckProperties.RibDepth = Convert.ToDouble(ribDepthMatch.Groups[1].Value);
                    }

                    // Other properties like RibWidthTop, RibWidthBottom, RibSpacing
                    var ribWidthTopMatch = Regex.Match(fullLine, @"DECKRIBWIDTHTOP\s+([\d\.]+)");
                    if (ribWidthTopMatch.Success)
                    {
                        floorProp.DeckProperties.RibWidthTop = Convert.ToDouble(ribWidthTopMatch.Groups[1].Value);
                    }

                    var ribWidthBottomMatch = Regex.Match(fullLine, @"DECKRIBWIDTHBOTTOM\s+([\d\.]+)");
                    if (ribWidthBottomMatch.Success)
                    {
                        floorProp.DeckProperties.RibWidthBottom = Convert.ToDouble(ribWidthBottomMatch.Groups[1].Value);
                    }

                    var ribSpacingMatch = Regex.Match(fullLine, @"DECKRIBSPACING\s+([\d\.]+)");
                    if (ribSpacingMatch.Success)
                    {
                        floorProp.DeckProperties.RibSpacing = Convert.ToDouble(ribSpacingMatch.Groups[1].Value);
                    }

                    // Deck thickness
                    var deckThicknessMatch = Regex.Match(fullLine, @"DECKSHEARTHICKNESS\s+([\d\.]+)");
                    if (deckThicknessMatch.Success)
                    {
                        floorProp.DeckProperties.DeckShearThickness = Convert.ToDouble(deckThicknessMatch.Groups[1].Value);
                    }

                    // Unit weight
                    var unitWeightMatch = Regex.Match(fullLine, @"DECKUNITWEIGHT\s+([\d\.]+)");
                    if (unitWeightMatch.Success)
                    {
                        floorProp.DeckProperties.DeckUnitWeight = Convert.ToDouble(unitWeightMatch.Groups[1].Value);
                    }

                    // Shear stud properties
                    var shearStudDiamMatch = Regex.Match(fullLine, @"SHEARSTUDDIAM\s+([\d\.]+)");
                    var shearStudHeightMatch = Regex.Match(fullLine, @"SHEARSTUDHEIGHT\s+([\d\.]+)");
                    var shearStudFuMatch = Regex.Match(fullLine, @"SHEARSTUDFU\s+([\d\.]+)");

                    if (shearStudDiamMatch.Success || shearStudHeightMatch.Success || shearStudFuMatch.Success)
                    {
                        floorProp.ShearStudProperties = new ShearStudProperties();

                        if (shearStudDiamMatch.Success)
                            floorProp.ShearStudProperties.ShearStudDiameter = Convert.ToDouble(shearStudDiamMatch.Groups[1].Value);

                        if (shearStudHeightMatch.Success)
                            floorProp.ShearStudProperties.ShearStudHeight = Convert.ToDouble(shearStudHeightMatch.Groups[1].Value);

                        if (shearStudFuMatch.Success)
                            floorProp.ShearStudProperties.ShearStudTensileStrength = Convert.ToDouble(shearStudFuMatch.Groups[1].Value);
                    }

                    floorProperties[name] = floorProp;
                }
            }
        }

        // Parse string modeling type to enum ModelingType
        private ModelingType ParseModelingType(string modelingTypeStr)
        {
            switch (modelingTypeStr.ToLower())
            {
                case "shellthick":
                    return ModelingType.ShellThick;
                case "membrane":
                    return ModelingType.Membrane;
                case "layered":
                    return ModelingType.Layered;
                case "shellthin":
                default:
                    return ModelingType.ShellThin;
            }
        }

        // Parse string slab type to enum SlabType
        private SlabType ParseSlabType(string slabTypeStr)
        {
            switch (slabTypeStr.ToLower())
            {
                case "drop":
                    return SlabType.Drop;
                case "stiff":
                    return SlabType.Stiff;
                case "ribbed":
                    return SlabType.Ribbed;
                case "waffle":
                    return SlabType.Waffle;
                case "mat":
                    return SlabType.Mat;
                case "footing":
                    return SlabType.Footing;
                case "slab":
                default:
                    return SlabType.Slab;
            }
        }
    }
}
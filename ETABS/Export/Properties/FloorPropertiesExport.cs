using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models;
using Core.Models.Properties;
using Core.Utilities;
using Core.Data;

namespace ETABS.Export.Properties
{
    // Imports floor property definitions from ETABS E2K file
    // Updated to use Core project's FloorPropertyProcessor and StructuralDeckData
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

                    // Use FloorPropertyProcessor to create standardized concrete slab properties
                    var floorProp = FloorPropertyProcessor.ProcessConcreteSlabProperties(
                        thickness,
                        materialId,
                        name); // Name taken straight from ETABS

                    // Set additional properties from ETABS parsing
                    floorProp.ModelingType = ParseModelingType(modelingTypeStr);
                    floorProp.SlabType = ParseSlabType(slabTypeStr);

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

            var lines = deckPropertiesSection.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var match = deckPattern.Match(line);
                if (match.Success && match.Groups.Count >= 6)
                {
                    string name = match.Groups[1].Value;
                    string deckTypeStr = match.Groups[2].Value;
                    string concreteMaterialName = match.Groups[3].Value;
                    string deckMaterialName = match.Groups[4].Value;
                    double slabDepth = Convert.ToDouble(match.Groups[5].Value);

                    // Look up concrete material ID
                    string concreteMaterialId = null;
                    if (_materialIdsByName.TryGetValue(concreteMaterialName, out string id))
                    {
                        concreteMaterialId = id;
                    }

                    // Parse additional deck properties from the line
                    var parsedDeckProps = ParseDeckPropertiesFromLine(line);

                    // Try to find matching deck from company standards using parsed properties
                    StructuralDeck foundDeck = null;
                    if (parsedDeckProps != null)
                    {
                        foundDeck = FloorPropertyProcessor.FindBestMatchingDeck(parsedDeckProps);
                    }

                    // If no match found, try by deck type name
                    if (foundDeck == null)
                    {
                        foundDeck = FloorPropertyProcessor.FindDeckByType(deckTypeStr);
                    }

                    // If still no match, try by rib depth from parsed properties
                    if (foundDeck == null && parsedDeckProps?.RibDepth > 0)
                    {
                        foundDeck = FloorPropertyProcessor.FindDeckByDepth(parsedDeckProps.RibDepth);
                    }

                    // Determine floor type based on ETABS deck type
                    StructuralFloorType floorType = DetermineFloorTypeFromETABSDeckType(deckTypeStr);

                    FloorProperties floorProp;

                    // Only use deck properties for FilledDeck and UnfilledDeck types
                    if ((floorType == StructuralFloorType.FilledDeck || floorType == StructuralFloorType.UnfilledDeck) && foundDeck != null)
                    {
                        // Use FloorPropertyProcessor with company standard deck
                        // Name taken straight from ETABS
                        floorProp = FloorPropertyProcessor.ProcessFromStructuralDeck(
                            foundDeck,
                            slabDepth,
                            concreteMaterialId,
                            floorType,
                            name);
                    }
                    else
                    {
                        // Create basic properties - DeckProperties will be null for Slab/SolidSlabDeck types
                        // or when no company standard deck is found for FilledDeck/UnfilledDeck
                        floorProp = new FloorProperties
                        {
                            Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                            Name = name, // Name taken straight from ETABS
                            Type = floorType,
                            MaterialId = concreteMaterialId,
                            ModelingType = ModelingType.Membrane,
                            SlabType = SlabType.Slab,
                            Thickness = floorType == StructuralFloorType.Slab ? slabDepth :
                                       (parsedDeckProps?.RibDepth > 0 ? slabDepth + parsedDeckProps.RibDepth : slabDepth),
                            DeckProperties = null // Null for Slab/SolidSlabDeck or when no company standard found
                        };
                    }

                    // Apply shear stud properties only if found in ETABS data
                    var shearStudProps = ParseShearStudProperties(line);
                    if (shearStudProps != null)
                    {
                        floorProp.ShearStudProperties = shearStudProps;
                    }
                    // Otherwise leave as null

                    floorProperties[name] = floorProp;
                }
            }
        }

        // Parse deck properties from ETABS E2K line
        private DeckProperties ParseDeckPropertiesFromLine(string line)
        {
            var deckProps = new DeckProperties();
            bool hasProperties = false;

            // Parse rib depth
            var ribDepthMatch = Regex.Match(line, @"DECKRIBDEPTH\s+([\d\.]+)");
            if (ribDepthMatch.Success)
            {
                deckProps.RibDepth = Convert.ToDouble(ribDepthMatch.Groups[1].Value);
                hasProperties = true;
            }

            // Parse rib width top
            var ribWidthTopMatch = Regex.Match(line, @"DECKRIBWIDTHTOP\s+([\d\.]+)");
            if (ribWidthTopMatch.Success)
            {
                deckProps.RibWidthTop = Convert.ToDouble(ribWidthTopMatch.Groups[1].Value);
                hasProperties = true;
            }

            // Parse rib width bottom
            var ribWidthBottomMatch = Regex.Match(line, @"DECKRIBWIDTHBOTTOM\s+([\d\.]+)");
            if (ribWidthBottomMatch.Success)
            {
                deckProps.RibWidthBottom = Convert.ToDouble(ribWidthBottomMatch.Groups[1].Value);
                hasProperties = true;
            }

            // Parse rib spacing
            var ribSpacingMatch = Regex.Match(line, @"DECKRIBSPACING\s+([\d\.]+)");
            if (ribSpacingMatch.Success)
            {
                deckProps.RibSpacing = Convert.ToDouble(ribSpacingMatch.Groups[1].Value);
                hasProperties = true;
            }

            // Parse deck thickness
            var deckThicknessMatch = Regex.Match(line, @"DECKSHEARTHICKNESS\s+([\d\.]+)");
            if (deckThicknessMatch.Success)
            {
                deckProps.DeckShearThickness = Convert.ToDouble(deckThicknessMatch.Groups[1].Value);
                hasProperties = true;
            }

            // Parse unit weight
            var unitWeightMatch = Regex.Match(line, @"DECKUNITWEIGHT\s+([\d\.]+)");
            if (unitWeightMatch.Success)
            {
                deckProps.DeckUnitWeight = Convert.ToDouble(unitWeightMatch.Groups[1].Value);
                hasProperties = true;
            }

            return hasProperties ? deckProps : null;
        }

        // Parse and return shear stud properties if found, otherwise return null
        private ShearStudProperties ParseShearStudProperties(string line)
        {
            var shearStudDiamMatch = Regex.Match(line, @"SHEARSTUDDIAM\s+([\d\.]+)");
            var shearStudHeightMatch = Regex.Match(line, @"SHEARSTUDHEIGHT\s+([\d\.]+)");
            var shearStudFuMatch = Regex.Match(line, @"SHEARSTUDFU\s+([\d\.]+)");

            if (shearStudDiamMatch.Success || shearStudHeightMatch.Success || shearStudFuMatch.Success)
            {
                var shearStudProps = new ShearStudProperties();

                if (shearStudDiamMatch.Success)
                {
                    shearStudProps.ShearStudDiameter = Convert.ToDouble(shearStudDiamMatch.Groups[1].Value);
                }

                if (shearStudHeightMatch.Success)
                {
                    shearStudProps.ShearStudHeight = Convert.ToDouble(shearStudHeightMatch.Groups[1].Value);
                }

                if (shearStudFuMatch.Success)
                {
                    shearStudProps.ShearStudTensileStrength = Convert.ToDouble(shearStudFuMatch.Groups[1].Value);
                }

                return shearStudProps;
            }

            return null; // No shear stud properties found
        }

        // Determine StructuralFloorType from ETABS deck type string
        private StructuralFloorType DetermineFloorTypeFromETABSDeckType(string deckType)
        {
            if (string.IsNullOrEmpty(deckType))
                return StructuralFloorType.FilledDeck;

            switch (deckType.ToLowerInvariant())
            {
                case "filled":
                    return StructuralFloorType.FilledDeck;
                case "unfilled":
                    return StructuralFloorType.UnfilledDeck;
                case "solid slab":
                    return StructuralFloorType.Slab;
                default:
                    return StructuralFloorType.FilledDeck; // Default assumption
            }
        }



        // Helper method to parse modeling type (preserved from original)
        private ModelingType ParseModelingType(string modelingTypeStr)
        {
            if (string.IsNullOrEmpty(modelingTypeStr))
                return ModelingType.Membrane;

            switch (modelingTypeStr.ToLowerInvariant())
            {
                case "membrane":
                    return ModelingType.Membrane;
                case "shellthin":
                    return ModelingType.ShellThin;
                case "shellthick":
                    return ModelingType.ShellThick;
                case "layered":
                    return ModelingType.Layered;
                default:
                    return ModelingType.Membrane;
            }
        }

        // Helper method to parse slab type (preserved from original)
        private SlabType ParseSlabType(string slabTypeStr)
        {
            if (string.IsNullOrEmpty(slabTypeStr))
                return SlabType.Slab;

            switch (slabTypeStr.ToLowerInvariant())
            {
                case "slab":
                    return SlabType.Slab;
                case "drop":
                    return SlabType.Drop;
                case "stiff":
                    return SlabType.Stiff;
                case "ribbed":
                    return SlabType.Ribbed;
                case "waffle":
                    return SlabType.Waffle;
                case "footing":
                    return SlabType.Footing;
                default:
                    return SlabType.Slab;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        public List<FloorProperties> Import(string slabPropertiesSection, string deckPropertiesSection)
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
                    string modelingType = match.Groups[3].Value;
                    string slabType = match.Groups[4].Value;
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
                        Type = "Slab",
                        Thickness = thickness,
                        MaterialId = materialId
                    };

                    // Add slab-specific properties
                    floorProp.SlabProperties["modelingType"] = modelingType;
                    floorProp.SlabProperties["slabType"] = slabType;

                    // Add specific slab type properties
                    switch (slabType.ToLower())
                    {
                        case "waffle":
                            floorProp.SlabProperties["isWaffle"] = true;
                            floorProp.SlabProperties["isTwoWay"] = true;
                            break;
                        case "ribbed":
                            floorProp.SlabProperties["isRibbed"] = true;
                            floorProp.SlabProperties["isTwoWay"] = false;
                            break;
                        default: // Regular slab
                            floorProp.SlabProperties["isWaffle"] = false;
                            floorProp.SlabProperties["isRibbed"] = false;
                            floorProp.SlabProperties["isTwoWay"] = true;
                            break;
                    }

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
            // Format: SHELLPROP "name" PROPTYPE "Deck" DECKTYPE "type" CONCMATERIAL "concMat" DECKMATERIAL "deckMat" DECKSLABDEPTH slabDepth DECKRIBDEPTH ribDepth ...
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
                    string floorType;
                    if (deckType.ToLower() == "filled")
                    {
                        floorType = "Composite";
                    }
                    else
                    {
                        floorType = "NonComposite";
                    }

                    // Create floor properties
                    var floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = name,
                        Type = floorType,
                        Thickness = slabDepth, // Use slab depth as total thickness
                        MaterialId = materialId
                    };

                    // Add deck-specific properties
                    floorProp.DeckProperties["deckType"] = deckType;
                    floorProp.DeckProperties["deckMaterialName"] = deckMaterialName;

                    // Extract additional deck properties if available in the match line
                    string fullLine = match.Value;

                    // Rib depth
                    var ribDepthMatch = Regex.Match(fullLine, @"DECKRIBDEPTH\s+([\d\.]+)");
                    if (ribDepthMatch.Success)
                    {
                        floorProp.DeckProperties["deckDepth"] = Convert.ToDouble(ribDepthMatch.Groups[1].Value);
                    }

                    // Deck gage
                    var deckGageMatch = Regex.Match(fullLine, @"DECKSHEARTHICKNESS\s+([\d\.]+)");
                    if (deckGageMatch.Success)
                    {
                        double shearThickness = Convert.ToDouble(deckGageMatch.Groups[1].Value);
                        // Convert shear thickness to approximate gage
                        floorProp.DeckProperties["deckGage"] = ConvertThicknessToGage(shearThickness);
                    }

                    floorProperties[name] = floorProp;
                }
            }
        }

        private int ConvertThicknessToGage(double thickness)
        {
            // Approximate conversion from thickness in inches to standard gage
            if (thickness >= 0.06) return 16;
            if (thickness >= 0.048) return 18;
            if (thickness >= 0.036) return 20;
            if (thickness >= 0.03) return 22;
            if (thickness >= 0.027) return 24;
            if (thickness >= 0.024) return 26;
            if (thickness >= 0.02) return 28;
            return 30;
        }
    }
}
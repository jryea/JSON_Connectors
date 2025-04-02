using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Properties;

namespace ETABS.Export.Properties
{
    /// <summary>
    /// Converts Core FloorProperties objects to ETABS E2K format text for both slab and deck properties
    /// </summary>
    public class FloorPropertiesExport
    {
        private IEnumerable<Material> _materials;

        /// <summary>
        /// Converts a collection of FloorProperties objects to E2K format text
        /// </summary>
        /// <param name="floorProperties">Collection of FloorProperties objects</param>
        /// <param name="materials">Collection of Material objects for reference</param>
        /// <returns>E2K format text for slab and deck properties</returns>
        public string ConvertToE2K(IEnumerable<FloorProperties> floorProperties, IEnumerable<Material> materials)
        {
            _materials = materials;
            StringBuilder sb = new StringBuilder();

            if (floorProperties == null || !floorProperties.Any())
                return string.Empty;

            // Separate properties into slabs and decks
            var slabProperties = floorProperties.Where(fp => IsSlabType(fp.Type));
            var deckProperties = floorProperties.Where(fp => IsDeckType(fp.Type));

            // Process slab properties
            if (slabProperties.Any())
            {
                sb.AppendLine("$ SLAB PROPERTIES");
                foreach (var slabProp in slabProperties)
                {
                    string formattedProp = FormatSlabProperty(slabProp);
                    sb.AppendLine(formattedProp);
                }
                sb.AppendLine();
            }

            // Process deck properties
            if (deckProperties.Any())
            {
                sb.AppendLine("$ DECK PROPERTIES");
                foreach (var deckProp in deckProperties)
                {
                    string formattedProp = FormatDeckProperty(deckProp);
                    sb.AppendLine(formattedProp);
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Determines if a floor type is a slab
        /// </summary>
        private bool IsSlabType(string floorType)
        {
            if (string.IsNullOrEmpty(floorType))
                return true; // Default to slab if type is not specified

            return floorType.ToLower() == "slab" ||
                   floorType.ToLower() == "waffle" ||
                   floorType.ToLower() == "ribbed" ||
                   floorType.ToLower() == "flat";
        }

        /// <summary>
        /// Determines if a floor type is a deck
        /// </summary>
        private bool IsDeckType(string floorType)
        {
            if (string.IsNullOrEmpty(floorType))
                return false;

            return floorType.ToLower() == "composite" ||
                   floorType.ToLower() == "noncomposite" ||
                   floorType.ToLower() == "metaldeck" ||
                   floorType.ToLower() == "deck";
        }

        /// <summary>
        /// Formats a slab property for E2K format
        /// </summary>
        private string FormatSlabProperty(FloorProperties slabProp)
        {
            // Check for null or empty name
            if (string.IsNullOrEmpty(slabProp.Name))
            {
                slabProp.Name = $"{slabProp.Thickness} in Slab";
            }

            // Get Material
            string materialName = _materials.FirstOrDefault(m => m.Id == slabProp.MaterialId)?.Name ?? "Concrete";

            // Determine modeling type (default to ShellThin)
            string modelingType = "ShellThin";
            if (slabProp.SlabProperties != null &&
                slabProp.SlabProperties.ContainsKey("modelingType") &&
                slabProp.SlabProperties["modelingType"] is string specifiedType)
            {
                modelingType = specifiedType;
            }

            // Determine slab type (default to "Slab")
            string slabType = "Slab";
            if (slabProp.Type?.ToLower() == "waffle")
                slabType = "Waffle";
            else if (slabProp.Type?.ToLower() == "ribbed")
                slabType = "Ribbed";

            // Format: SHELLPROP "Slab1" PROPTYPE "Slab" MATERIAL "Concrete" MODELINGTYPE "ShellThin" SLABTYPE "Slab" SLABTHICKNESS 8
            return $"  SHELLPROP  \"{slabProp.Name}\"  PROPTYPE  \"Slab\"  MATERIAL \"{materialName}\"  " +
                   $"MODELINGTYPE \"{modelingType}\"  SLABTYPE \"{slabType}\"  SLABTHICKNESS {slabProp.Thickness}";
        }

        /// <summary>
        /// Formats a deck property for E2K format
        /// </summary>
        private string FormatDeckProperty(FloorProperties deckProp)
        {
            // Check for null or empty name
            if (string.IsNullOrEmpty(deckProp.Name))
            {
                deckProp.Name = $"Deck{deckProp.Thickness}";
            }

            // Initialize deck properties dictionary
            var deckPropDict = deckProp.DeckProperties ?? new Dictionary<string, object>();

            // Get concrete material for topping
            string concreteMaterial = _materials.FirstOrDefault(m => m.Id == deckProp.MaterialId)?.Name ?? "Concrete";

            // Determine deck material
            string deckMaterial = "Steel"; // Default
            if (deckPropDict.ContainsKey("deckMaterialId") && deckPropDict["deckMaterialId"] is string deckMatId)
            {
                // Try to find the deck material by ID
                var deckMat = _materials.FirstOrDefault(m => m.Id == deckMatId);
                if (deckMat != null)
                {
                    deckMaterial = deckMat.Name;
                }
            }
            else
            {
                // Try to find a steel material as fallback
                var steelMat = _materials.FirstOrDefault(m => m.Type?.ToLower() == "steel");
                if (steelMat != null)
                {
                    deckMaterial = steelMat.Name;
                }
            }

            // Determine deck type (default to "Filled")
            string deckType = "Filled";
            if (deckPropDict.ContainsKey("deckType") && deckPropDict["deckType"] is string specifiedType)
            {
                deckType = specifiedType;
            }
            else if (deckProp.Type?.ToLower() == "noncomposite")
            {
                deckType = "Unfilled";
            }

            // Get deck dimensions with defaults
            double deckSlabDepth = GetDeckProperty(deckPropDict, "toppingThickness", 3.5);
            double deckRibDepth = GetDeckProperty(deckPropDict, "deckDepth", 3.0);
            double deckRibWidthTop = GetDeckProperty(deckPropDict, "deckRibWidthTop", 7.0);
            double deckRibWidthBottom = GetDeckProperty(deckPropDict, "deckRibWidthBottom", 5.0);
            double deckRibSpacing = GetDeckProperty(deckPropDict, "deckRibSpacing", 12.0);
            double deckShearThickness = GetDeckProperty(deckPropDict, "deckShearThickness", 0.035);
            double deckUnitWeight = GetDeckProperty(deckPropDict, "deckUnitWeight", 0.01597222);
            double shearStudDiam = GetDeckProperty(deckPropDict, "shearStudDiam", 0.75);
            double shearStudHeight = GetDeckProperty(deckPropDict, "shearStudHeight", 6.0);
            double shearStudFu = GetDeckProperty(deckPropDict, "shearStudFu", 65000.0);

            // Format: SHELLPROP "Deck1" PROPTYPE "Deck" DECKTYPE "Filled" CONCMATERIAL "Concrete" DECKMATERIAL "Steel" ...
            return $"  SHELLPROP  \"{deckProp.Name}\"  PROPTYPE  \"Deck\"  DECKTYPE \"{deckType}\"  " +
                   $"CONCMATERIAL \"{concreteMaterial}\"  DECKMATERIAL \"{deckMaterial}\"  " +
                   $"DECKSLABDEPTH {deckSlabDepth} DECKRIBDEPTH {deckRibDepth} " +
                   $"DECKRIBWIDTHTOP {deckRibWidthTop} DECKRIBWIDTHBOTTOM {deckRibWidthBottom} " +
                   $"DECKRIBSPACING {deckRibSpacing} DECKSHEARTHICKNESS {deckShearThickness} " +
                   $"DECKUNITWEIGHT {deckUnitWeight} SHEARSTUDDIAM {shearStudDiam} " +
                   $"SHEARSTUDHEIGHT {shearStudHeight} SHEARSTUDFU {shearStudFu}";
        }

        /// <summary>
        /// Gets a property value from the deck properties dictionary with a default fallback
        /// </summary>
        private double GetDeckProperty(Dictionary<string, object> deckProperties, string propertyName, double defaultValue)
        {
            if (deckProperties.ContainsKey(propertyName))
            {
                if (deckProperties[propertyName] is double doubleValue)
                    return doubleValue;

                if (deckProperties[propertyName] is int intValue)
                    return intValue;

                if (deckProperties[propertyName] is string stringValue && double.TryParse(stringValue, out double parsedValue))
                    return parsedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Converts a single FloorProperties object to E2K format text
        /// </summary>
        public string ConvertToE2K(FloorProperties floorProp)
        {
            return ConvertToE2K(new[] { floorProp }, _materials);
        }
    }
}
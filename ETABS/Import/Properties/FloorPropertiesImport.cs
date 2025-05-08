using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Properties;

namespace ETABS.Import.Properties
{
    // Converts Core FloorProperties objects to ETABS E2K format text for slab and deck properties
    public class FloorPropertiesImport
    {
        private IEnumerable<Material> _materials;

        // Converts a collection of FloorProperties objects to E2K format text
        public string ConvertToE2K(IEnumerable<FloorProperties> floorProperties, IEnumerable<Material> materials)
        {
            _materials = materials;
            StringBuilder sb = new StringBuilder();

            // E2K Slab Properties Section Header
            sb.AppendLine("$ SLAB PROPERTIES");

            if (floorProperties == null || !floorProperties.Any())
            {
                // Add a default slab property
                sb.AppendLine("  SHELLPROP  \"Slab1\"  PROPTYPE  \"Slab\"  MATERIAL \"4000 psi\"  MODELINGTYPE \"ShellThin\"  SLABTYPE \"Slab\"  SLABTHICKNESS 8");
                return sb.ToString();
            }

            // Process each floor property
            foreach (var floorProp in floorProperties)
            {
                if (IsDeckProperty(floorProp))
                {
                    // Format and append deck property
                    string deckPropertyLine = FormatDeckProperty(floorProp);
                    sb.AppendLine(deckPropertyLine);
                }
                else
                {
                    // Format and append slab property
                    string slabPropertyLine = FormatSlabProperty(floorProp);
                    sb.AppendLine(slabPropertyLine);
                }
            }

            return sb.ToString();
        }

        // Formats a single FloorProperties object as E2K shell property for a standard slab
        private string FormatSlabProperty(FloorProperties floorProp)
        {
            // Check for null or empty name
            if (string.IsNullOrEmpty(floorProp.Name))
            {
                floorProp.Name = $"{floorProp.Thickness} inch slab";
            }

            // Replace Unicode representation of double quote (\u0022) with "inch" in the floor property name
            floorProp.Name = floorProp.Name.Replace("\u0022", " inch");

            // Get Material
            string materialName = _materials.FirstOrDefault(m => m.Id == floorProp.MaterialId)?.Name ?? "4000 psi";

            // Replace Unicode representation of double quote (\u0022) with "inch" in the material name
            materialName = materialName.Replace("\u0022", " inch");

            // Determine modeling type (default to ShellThin)
            string modelingType = "ShellThin";
            if (floorProp.SlabProperties != null &&
                floorProp.SlabProperties.ContainsKey("modelingType") &&
                floorProp.SlabProperties["modelingType"] is string specifiedType)
            {
                modelingType = specifiedType;
            }

            // Determine slab type (default to "Slab")
            string slabType = "Slab";
            if (floorProp.Type?.ToLower() == "waffle")
                slabType = "Waffle";
            else if (floorProp.Type?.ToLower() == "ribbed")
                slabType = "Ribbed";

            // Format: SHELLPROP "Slab1" PROPTYPE "Slab" MATERIAL "4000 psi" MODELINGTYPE "ShellThin" SLABTYPE "Slab" SLABTHICKNESS 8
            return $"  SHELLPROP  \"{floorProp.Name}\"  PROPTYPE  \"Slab\"  MATERIAL \"{materialName}\"  " +
                   $"MODELINGTYPE \"{modelingType}\"  SLABTYPE \"{slabType}\"  SLABTHICKNESS {floorProp.Thickness}";
        }

        // Formats a single FloorProperties object as E2K shell property for a deck
        private string FormatDeckProperty(FloorProperties floorProp)
        {
            // Check for null or empty name
            if (string.IsNullOrEmpty(floorProp.Name))
            {
                floorProp.Name = $"{floorProp.Type} Deck {floorProp.Thickness} inch";
            }

            // Replace Unicode representation of double quote (\u0022) with "inch" in the floor property name
            floorProp.Name = floorProp.Name.Replace("\u0022", " inch");

            // Get Concrete Material (main material for the deck)
            string concMaterial = _materials.FirstOrDefault(m => m.Id == floorProp.MaterialId)?.Name ?? "4000 psi";
            concMaterial = concMaterial.Replace("\u0022", " inch");

            // Get Deck Material (default to steel)
            string deckMaterial = _materials.FirstOrDefault(m => m.Type?.ToLower() == "steel")?.Name ?? "A992Fy50";
            deckMaterial = deckMaterial.Replace("\u0022", " inch");

            // Get deck properties with defaults
            double deckSlabDepth = floorProp.Thickness;
            double deckRibDepth = 3.0;
            double deckRibWidthTop = 7.0;
            double deckRibWidthBottom = 5.0;
            double deckRibSpacing = 12.0;
            double deckShearThickness = 0.035;
            double deckUnitWeight = 0.01597222;

            // Override with values from DeckProperties if available
            if (floorProp.DeckProperties != null)
            {
                if (floorProp.DeckProperties.ContainsKey("deckDepth") && floorProp.DeckProperties["deckDepth"] is double depth)
                    deckRibDepth = depth;

                if (floorProp.DeckProperties.ContainsKey("toppingThickness") && floorProp.DeckProperties["toppingThickness"] is double topping)
                    deckSlabDepth = topping;
            }

            // Determine deck type based on floor type
            string deckType = floorProp.Type?.ToLower() == "composite" ? "Filled" : "Unfilled";

            // Format: SHELLPROP "Deck1" PROPTYPE "Deck" DECKTYPE "Filled" CONCMATERIAL "4000Psi" DECKMATERIAL "A992Fy50" ...
            return $"  SHELLPROP  \"{floorProp.Name}\"  PROPTYPE  \"Deck\"  DECKTYPE \"{deckType}\"  " +
                   $"CONCMATERIAL \"{concMaterial}\"  DECKMATERIAL \"{deckMaterial}\"  " +
                   $"DECKSLABDEPTH {deckSlabDepth} DECKRIBDEPTH {deckRibDepth} " +
                   $"DECKRIBWIDTHTOP {deckRibWidthTop} DECKRIBWIDTHBOTTOM {deckRibWidthBottom} " +
                   $"DECKRIBSPACING {deckRibSpacing} DECKSHEARTHICKNESS {deckShearThickness} " +
                   $"DECKUNITWEIGHT {deckUnitWeight} SHEARSTUDDIAM 0.75 SHEARSTUDHEIGHT 6 SHEARSTUDFU 65000";
        }

        // Checks if floor properties represent a deck type
        private bool IsDeckProperty(FloorProperties floorProp)
        {
            if (string.IsNullOrEmpty(floorProp.Type))
                return false;

            return floorProp.Type.ToLower() == "composite" ||
                   floorProp.Type.ToLower() == "noncomposite";
        }

        // Converts a single FloorProperties object to E2K format text
        public string ConvertToE2K(FloorProperties floorProp)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Slab Properties Section Header
            sb.AppendLine("$ SLAB PROPERTIES");

            // Format and append the appropriate property based on type
            if (IsDeckProperty(floorProp))
            {
                string deckPropertyLine = FormatDeckProperty(floorProp);
                sb.AppendLine(deckPropertyLine);
            }
            else
            {
                string slabPropertyLine = FormatSlabProperty(floorProp);
                sb.AppendLine(slabPropertyLine);
            }

            return sb.ToString();
        }
    }
}
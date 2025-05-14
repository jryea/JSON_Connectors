using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models;
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
            sb.AppendLine("$ SLAB PROPERTIES");

            if (floorProperties == null || !floorProperties.Any())
            {
                sb.AppendLine("  SHELLPROP  \"Slab1\"  PROPTYPE  \"Slab\"  MATERIAL \"4000 psi\"  MODELINGTYPE \"ShellThin\"  SLABTYPE \"Slab\"  SLABTHICKNESS 8");
                return sb.ToString();
            }

            foreach (var floorProp in floorProperties)
            {
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
            materialName = materialName.Replace("\u0022", " inch");

            // Convert ModelingType enum to ETABS string
            string modelingTypeStr = GetModelingTypeString(floorProp.ModelingType);

            // Convert SlabType enum to ETABS string
            string slabTypeStr = GetSlabTypeString(floorProp.SlabType);

            return $"  SHELLPROP  \"{floorProp.Name}\"  PROPTYPE  \"Slab\"  MATERIAL \"{materialName}\"  " +
                   $"MODELINGTYPE \"{modelingTypeStr}\"  SLABTYPE \"{slabTypeStr}\"  SLABTHICKNESS {floorProp.Thickness}";
        }

        // Formats a single FloorProperties object as E2K shell property for a deck
        private string FormatDeckProperty(FloorProperties floorProp)
        {
            // Check for null or empty name
            if (string.IsNullOrEmpty(floorProp.Name))
            {
                floorProp.Name = $"{floorProp.Type} Deck {floorProp.Thickness} inch";
            }

            // Replace Unicode representation of double quote with "inch"
            string formattedName = floorProp.Name.Replace("\u0022", " inch");

            // Get Concrete Material (main material for the deck)
            string concMaterial = _materials.FirstOrDefault(m => m.Id == floorProp.MaterialId)?.Name ?? "4000 psi";
            concMaterial = concMaterial.Replace("\u0022", " inch");

            // Get Deck Material (default to steel)
            string deckMaterial = _materials.FirstOrDefault(m => m.Type == MaterialType.Steel)?.Name ?? "A992Fy50";
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
                deckRibDepth = floorProp.DeckProperties.RibDepth > 0 ? floorProp.DeckProperties.RibDepth : deckRibDepth;
                deckRibWidthTop = floorProp.DeckProperties.RibWidthTop > 0 ? floorProp.DeckProperties.RibWidthTop : deckRibWidthTop;
                deckRibWidthBottom = floorProp.DeckProperties.RibWidthBottom > 0 ? floorProp.DeckProperties.RibWidthBottom : deckRibWidthBottom;
                deckRibSpacing = floorProp.DeckProperties.RibSpacing > 0 ? floorProp.DeckProperties.RibSpacing : deckRibSpacing;
                deckShearThickness = floorProp.DeckProperties.DeckShearThickness > 0 ? floorProp.DeckProperties.DeckShearThickness : deckShearThickness;
                deckUnitWeight = floorProp.DeckProperties.DeckUnitWeight > 0 ? floorProp.DeckProperties.DeckUnitWeight : deckUnitWeight;
            }

            // Get shear stud properties with defaults
            double shearStudDiam = 0.75;
            double shearStudHeight = 6.0;
            double shearStudFu = 65000.0;

            // Override with values from ShearStudProperties if available
            if (floorProp.ShearStudProperties != null)
            {
                shearStudDiam = floorProp.ShearStudProperties.ShearStudDiameter > 0 ? floorProp.ShearStudProperties.ShearStudDiameter : shearStudDiam;
                shearStudHeight = floorProp.ShearStudProperties.ShearStudHeight > 0 ? floorProp.ShearStudProperties.ShearStudHeight : shearStudHeight;
                shearStudFu = floorProp.ShearStudProperties.ShearStudTensileStrength > 0 ? floorProp.ShearStudProperties.ShearStudTensileStrength : shearStudFu;
            }

            // Determine deck type based on floor type enum
            string deckType = floorProp.Type == StructuralFloorType.FilledDeck ? "Filled" : "Unfilled";

            // Format: SHELLPROP "Deck1" PROPTYPE "Deck" DECKTYPE "Filled" CONCMATERIAL "4000Psi" DECKMATERIAL "A992Fy50" ...
            return $"  SHELLPROP  \"{formattedName}\"  PROPTYPE  \"Deck\"  DECKTYPE \"{deckType}\"  " +
                   $"CONCMATERIAL \"{concMaterial}\"  DECKMATERIAL \"{deckMaterial}\"  " +
                   $"DECKSLABDEPTH {deckSlabDepth} DECKRIBDEPTH {deckRibDepth} " +
                   $"DECKRIBWIDTHTOP {deckRibWidthTop} DECKRIBWIDTHBOTTOM {deckRibWidthBottom} " +
                   $"DECKRIBSPACING {deckRibSpacing} DECKSHEARTHICKNESS {deckShearThickness} " +
                   $"DECKUNITWEIGHT {deckUnitWeight} SHEARSTUDDIAM {shearStudDiam} SHEARSTUDHEIGHT {shearStudHeight} SHEARSTUDFU {shearStudFu}";
        }

        // Checks if floor properties represent a deck type
        private bool IsDeckProperty(FloorProperties floorProp)
        {
            // Check if FloorType enum indicates a deck type
            return floorProp.Type == StructuralFloorType.FilledDeck ||
                   floorProp.Type == StructuralFloorType.UnfilledDeck ||
                   floorProp.Type == StructuralFloorType.SolidSlabDeck;
        }

        private string GetModelingTypeString(ModelingType modelingType)
        {
            switch (modelingType)
            {
                case ModelingType.ShellThick:
                    return "ShellThick";
                case ModelingType.Membrane:
                    return "Membrane";
                case ModelingType.Layered:
                    return "Layered";
                default:
                    return "ShellThin";
            }
        }

        private string GetSlabTypeString(SlabType slabType)
        {
            switch (slabType)
            {
                case SlabType.Drop:
                    return "Drop";
                case SlabType.Stiff:
                    return "Stiff";
                case SlabType.Ribbed:
                    return "Ribbed";
                case SlabType.Waffle:
                    return "Waffle";
                case SlabType.Mat:
                    return "Mat";
                case SlabType.Footing:
                    return "Footing";
                default:
                    return "Slab";
            }
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
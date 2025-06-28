// Revit/Utilities/FloorTypeNameGenerator.cs
using Core.Models;
using Core.Models.Properties;

namespace Revit.Utilities
{
    /// <summary>
    /// Generates Revit-specific floor type names based on floor properties
    /// </summary>
    public static class FloorTypeNameGenerator
    {
        /// <summary>
        /// Generates a Revit floor type name based on floor properties
        /// </summary>
        public static string GenerateRevitFloorTypeName(FloorProperties floorProps)
        {
            switch (floorProps.Type)
            {
                case StructuralFloorType.FilledDeck:
                    var concreteThickness = floorProps.Thickness - floorProps.DeckProperties.RibDepth;
                    return $"{concreteThickness}\" Concrete on {floorProps.DeckProperties.DeckType}";

                case StructuralFloorType.UnfilledDeck:
                    return floorProps.DeckProperties.DeckType;

                case StructuralFloorType.Slab:
                default:
                    return $"{floorProps.Thickness}\" Concrete";
            }
        }

        /// <summary>
        /// Generates a simplified Revit floor type name (without deck name)
        /// </summary>
        public static string GenerateSimplifiedRevitFloorTypeName(FloorProperties floorProps)
        {
            switch (floorProps.Type)
            {
                case StructuralFloorType.FilledDeck:
                    var concreteThickness = floorProps.Thickness - floorProps.DeckProperties.RibDepth;
                    var deckThickness = floorProps.DeckProperties.RibDepth;
                    return $"{concreteThickness}\" Concrete on {deckThickness}\" Metal Deck";

                case StructuralFloorType.UnfilledDeck:
                    return $"{floorProps.DeckProperties.RibDepth}\" Metal Deck";

                case StructuralFloorType.Slab:
                default:
                    return $"{floorProps.Thickness}\" Concrete";
            }
        }
    }
}
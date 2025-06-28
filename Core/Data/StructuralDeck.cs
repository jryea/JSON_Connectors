// Core/Models/Data/StructuralDeck.cs
using System;

namespace Core.Data
{
    /// <summary>
    /// Represents a structural deck type with standardized properties
    /// Maps to DeckProperties for consistency
    /// </summary>
    public class StructuralDeck
    {
        /// <summary>
        /// Deck name/type (e.g., "VULCRAFT 2VL")
        /// </summary>
        public string DeckType { get; set; }

        /// <summary>
        /// Rib depth in inches (Hr in deck tables)
        /// </summary>
        public double RibDepth { get; set; }

        /// <summary>
        /// Rib width at top in inches (Wr in deck tables)
        /// </summary>
        public double RibWidthTop { get; set; }

        /// <summary>
        /// Rib spacing in inches
        /// </summary>
        public double RibSpacing { get; set; }

        /// <summary>
        /// Calculated rib width at bottom in inches
        /// </summary>
        public double RibWidthBottom => RibSpacing - RibWidthTop;

        /// <summary>
        /// Optional manufacturer name
        /// </summary>
        public string Manufacturer { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Properties;
using Core.Data;
using Core.Utilities;
using Core.Models;

namespace Core.Utilities
{
    /// <summary>
    /// Processes floor data from various sources into standardized FloorProperties
    /// Now uses StructuralDeckData for company standard deck information
    /// </summary>
    public static class FloorPropertyProcessor
    {
        /// <summary>
        /// Creates FloorProperties from a StructuralDeck and additional parameters
        /// </summary>
        public static FloorProperties ProcessFromStructuralDeck(
            StructuralDeck deck,
            double concreteThickness,
            string concreteMaterialId,
            StructuralFloorType floorType,
            string name = null)
        {
            if (deck == null)
                throw new ArgumentNullException(nameof(deck));

            var floorProps = new FloorProperties
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                Name = name ?? deck.DeckType, // Default to deck type name if no name provided
                Type = floorType,
                MaterialId = concreteMaterialId,
                ModelingType = ModelingType.Membrane,
                SlabType = SlabType.Slab
            };

            // Calculate total thickness
            if (floorType == StructuralFloorType.FilledDeck)
            {
                floorProps.Thickness = concreteThickness + deck.RibDepth;
            }
            else if (floorType == StructuralFloorType.UnfilledDeck)
            {
                floorProps.Thickness = deck.RibDepth;
            }
            else // Slab
            {
                floorProps.Thickness = concreteThickness;
            }

            // Populate deck properties
            if (floorType != StructuralFloorType.Slab)
            {
                floorProps.DeckProperties = new DeckProperties
                {
                    DeckType = deck.DeckType,
                    RibDepth = deck.RibDepth,
                    RibWidthTop = deck.RibWidthTop,
                    RibWidthBottom = deck.RibWidthBottom,
                    RibSpacing = deck.RibSpacing,
                    DeckShearThickness = 0,  // 0 to indicate we are not getting this value from anywhere
                    DeckUnitWeight = 0  // 0 to indicate we are not getting this value from anywhere
                };
            }

            return floorProps;
        }

        /// <summary>
        /// Find deck by name using company standards
        /// </summary>
        public static StructuralDeck FindDeckByType(string deckType)
        {
            return StructuralDeck.FindByType(deckType);
        }

        /// <summary>
        /// Create FloorProperties from deck type name and parameters
        /// Convenience method for exporters - finds deck automatically
        /// </summary>
        public static FloorProperties ProcessFromDeckType(
            string deckType,
            double concreteThickness,
            string concreteMaterialId,
            StructuralFloorType floorType,
            string name = null)
        {
            var deck = StructuralDeck.FindByType(deckType);

            if (deck == null)
            {
                // Fallback - try to find by properties if exact name doesn't match
                Console.WriteLine($"Warning: Deck type '{deckType}' not found, using default 2\" deck");
                deck = StructuralDeck.GetPreferredDeck(2.0); // Default to 2" deck
            }

            return ProcessFromStructuralDeck(deck, concreteThickness, concreteMaterialId, floorType, name);
        }

        /// <summary>
        /// Get company preferred deck for a given depth
        /// </summary>
        public static StructuralDeck GetPreferredDeck(double ribDepth)
        {
            return StructuralDeck.GetPreferredDeck(ribDepth);
        }

        /// <summary>
        /// Get all available company standard decks
        /// </summary>
        public static List<StructuralDeck> GetAvailableDecks()
        {
            return new List<StructuralDeck>(StructuralDeck.StandardDecks);
        }

        /// <summary>
        /// Finds the best matching deck from company standards based on deck properties
        /// Used for ETABS export where we have properties but need a deck name
        /// </summary>
        public static StructuralDeck FindBestMatchingDeck(DeckProperties deckProps)
        {
            if (deckProps == null)
                return null;

            return StructuralDeck.FindBestMatch(
                deckProps.RibDepth,
                deckProps.RibSpacing,
                deckProps.RibWidthTop);
        }

        /// <summary>
        /// Finds the best matching deck from a specific list based on deck properties
        /// Used for ETABS export where we have properties but need a deck name
        /// </summary>
        public static StructuralDeck FindBestMatchingDeck(
            DeckProperties deckProps,
            IEnumerable<StructuralDeck> availableDecks)
        {
            if (deckProps == null || availableDecks == null || !availableDecks.Any())
                return null;

            var scoredDecks = availableDecks
                .Select(deck => new
                {
                    Deck = deck,
                    Score = deck.CalculateMatchScore(deckProps.RibDepth, deckProps.RibSpacing, deckProps.RibWidthTop)
                })
                .OrderBy(x => x.Score)
                .ToList();

            // Return best match if score is reasonable (threshold can be adjusted)
            var bestMatch = scoredDecks.FirstOrDefault();
            if (bestMatch != null && bestMatch.Score < 5.0) // Threshold for acceptable match
            {
                return bestMatch.Deck;
            }

            return null;
        }

        /// <summary>
        /// Creates FloorProperties for a concrete slab (no deck)
        /// </summary>
        public static FloorProperties ProcessConcreteSlabProperties(
            double thickness,
            string materialId,
            string name = null)
        {
            return new FloorProperties
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                Name = name ?? $"Slab {thickness}\"",
                Type = StructuralFloorType.Slab,
                Thickness = thickness,
                MaterialId = materialId,
                ModelingType = ModelingType.Membrane,
                SlabType = SlabType.Slab
            };
        }

        /// <summary>
        /// Find deck by approximate rib depth (within tolerance)
        /// Useful when only thickness is known
        /// </summary>
        public static StructuralDeck FindDeckByDepth(double ribDepth, double tolerance = 0.25)
        {
            return StructuralDeck.FindByDepth(ribDepth, tolerance);
        }

        /// <summary>
        /// Get available manufacturers for UI/selection purposes
        /// </summary>
        public static List<string> GetAvailableManufacturers()
        {
            return StructuralDeck.GetManufacturers();
        }

        /// <summary>
        /// Get decks by manufacturer for UI/selection purposes
        /// </summary>
        public static List<StructuralDeck> GetDecksByManufacturer(string manufacturer)
        {
            return StructuralDeck.GetByManufacturer(manufacturer);
        }
    }
}
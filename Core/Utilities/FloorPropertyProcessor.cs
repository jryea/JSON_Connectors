// Core/Processors/FloorPropertyProcessor.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Properties;
using Core.Data;
using Core.Utilities;
using Core.Models;

namespace Core.Processors
{
    /// <summary>
    /// Processes floor data from various sources into standardized FloorProperties
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
        /// Finds the best matching deck from a list based on deck properties
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
                    Score = CalculateDeckMatchScore(deck, deckProps)
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
        /// Calculates a match score between a structural deck and deck properties
        /// Lower score = better match
        /// </summary>
        private static double CalculateDeckMatchScore(
            StructuralDeck deck,
            DeckProperties props)
        {
            double score = 0;

            // Primary criterion: Rib depth (most important)
            if (props.RibDepth > 0)
            {
                score += Math.Abs(deck.RibDepth - props.RibDepth) * 10.0;
            }

            // Secondary criterion: Rib spacing
            if (props.RibSpacing > 0)
            {
                score += Math.Abs(deck.RibSpacing - props.RibSpacing) * 5.0;
            }

            // Tertiary criterion: Rib width top
            if (props.RibWidthTop > 0)
            {
                score += Math.Abs(deck.RibWidthTop - props.RibWidthTop) * 3.0;
            }

            // Check rib width bottom if available
            if (props.RibWidthBottom > 0)
            {
                score += Math.Abs(deck.RibWidthBottom - props.RibWidthBottom) * 2.0;
            }

            return score;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Data
{
    /// <summary>
    /// Comprehensive structural deck data and operations for company standards
    /// Contains the data model, static deck library, and all deck-related helper methods
    /// </summary>
    public class StructuralDeck
    {
        #region Data Model Properties

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
        /// Manufacturer name
        /// </summary>
        public string Manufacturer { get; set; }

        #endregion

        #region Static Data Library

        /// <summary>
        /// Complete library of company standard structural decks
        /// Prioritized by manufacturer preference (VULCRAFT first, then VERCO, etc.)
        /// </summary>
        public static readonly List<StructuralDeck> StandardDecks = new List<StructuralDeck>
        {
            // VULCRAFT - Primary company standard
            new StructuralDeck
            {
                DeckType = "VULCRAFT 3VL",
                RibDepth = 3.0,
                RibWidthTop = 7.25,
                RibSpacing = 12.0,
                Manufacturer = "VULCRAFT"
            },
            new StructuralDeck
            {
                DeckType = "VULCRAFT 2VL",
                RibDepth = 2.0,
                RibWidthTop = 7.0,
                RibSpacing = 12.0,
                Manufacturer = "VULCRAFT"
            },
            new StructuralDeck
            {
                DeckType = "VULCRAFT 1.5VL",
                RibDepth = 1.5,
                RibWidthTop = 2.625,
                RibSpacing = 6.0,
                Manufacturer = "VULCRAFT"
            },
            new StructuralDeck
            {
                DeckType = "VULCRAFT 1.5VLR",
                RibDepth = 1.5,
                RibWidthTop = 3.875,
                RibSpacing = 6.0,
                Manufacturer = "VULCRAFT"
            },

            // VERCO - Secondary standard
            new StructuralDeck
            {
                DeckType = "VERCO W3 Formlok",
                RibDepth = 3.0,
                RibWidthTop = 6.0,
                RibSpacing = 12.0,
                Manufacturer = "VERCO"
            },
            new StructuralDeck
            {
                DeckType = "VERCO W2 Formlok",
                RibDepth = 2.0625,
                RibWidthTop = 6.0,
                RibSpacing = 12.0,
                Manufacturer = "VERCO"
            },
            new StructuralDeck
            {
                DeckType = "VERCO B Formlok",
                RibDepth = 1.5,
                RibWidthTop = 2.125,
                RibSpacing = 6.0,
                Manufacturer = "VERCO"
            },
            new StructuralDeck
            {
                DeckType = "VERCO BR Formlok",
                RibDepth = 1.5,
                RibWidthTop = 3.875,
                RibSpacing = 6.0,
                Manufacturer = "VERCO"
            },
            new StructuralDeck
            {
                DeckType = "VERCO N3 Formlok",
                RibDepth = 3.0,
                RibWidthTop = 3.125,
                RibSpacing = 8.0,
                Manufacturer = "VERCO"
            },

            // ASC - Alternate standard
            new StructuralDeck
            {
                DeckType = "ASC 2WH",
                RibDepth = 2.125,
                RibWidthTop = 6.0,
                RibSpacing = 12.0,
                Manufacturer = "ASC"
            },
            new StructuralDeck
            {
                DeckType = "ASC 3WxH",
                RibDepth = 3.0,
                RibWidthTop = 6.0,
                RibSpacing = 12.0,
                Manufacturer = "ASC"
            },
            new StructuralDeck
            {
                DeckType = "ASC BH",
                RibDepth = 1.5,
                RibWidthTop = 2.141,
                RibSpacing = 6.0,
                Manufacturer = "ASC"
            },
            new StructuralDeck
            {
                DeckType = "ASC NH",
                RibDepth = 3.0,
                RibWidthTop = 3.125,
                RibSpacing = 8.0,
                Manufacturer = "ASC"
            },

            // NewMillennium - Alternate standard
            new StructuralDeck
            {
                DeckType = "NewMillennium 3.0CD",
                RibDepth = 3.0,
                RibWidthTop = 6.0,
                RibSpacing = 12.0,
                Manufacturer = "NewMillennium"
            },
            new StructuralDeck
            {
                DeckType = "NewMillennium 2.0CD",
                RibDepth = 2.0,
                RibWidthTop = 6.0,
                RibSpacing = 12.0,
                Manufacturer = "NewMillennium"
            },
            new StructuralDeck
            {
                DeckType = "NewMillennium 1.5CD",
                RibDepth = 1.5,
                RibWidthTop = 2.063,
                RibSpacing = 6.0,
                Manufacturer = "NewMillennium"
            },

            // USD - Additional standard
            new StructuralDeck
            {
                DeckType = "USD 3.0 LokFloor",
                RibDepth = 3.0,
                RibWidthTop = 6.0,
                RibSpacing = 12.0,
                Manufacturer = "USD"
            },
            new StructuralDeck
            {
                DeckType = "USD 2.0 LokFloor",
                RibDepth = 2.0,
                RibWidthTop = 6.0,
                RibSpacing = 12.0,
                Manufacturer = "USD"
            },
            new StructuralDeck
            {
                DeckType = "USD 1.5 LokFloor",
                RibDepth = 1.5,
                RibWidthTop = 6.0,
                RibSpacing = 12.0,
                Manufacturer = "USD"
            },
            new StructuralDeck
            {
                DeckType = "USD 1.5 B-Lok",
                RibDepth = 1.5,
                RibWidthTop = 2.25,
                RibSpacing = 6.0,
                Manufacturer = "USD"
            },

            // Generic/Fallback
            new StructuralDeck
            {
                DeckType = "Flat Slab",
                RibDepth = 0.0001,
                RibWidthTop = 1.0,
                RibSpacing = 0.0001,
                Manufacturer = "Generic"
            }
        };

        #endregion

        #region Static Helper Methods

        /// <summary>
        /// Find deck by exact type name (case-insensitive)
        /// </summary>
        public static StructuralDeck FindByType(string deckType)
        {
            if (string.IsNullOrWhiteSpace(deckType))
                return null;

            return StandardDecks.FirstOrDefault(d =>
                d.DeckType.Equals(deckType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get decks by manufacturer
        /// </summary>
        public static List<StructuralDeck> GetByManufacturer(string manufacturer)
        {
            if (string.IsNullOrWhiteSpace(manufacturer))
                return new List<StructuralDeck>();

            return StandardDecks
                .Where(d => d.Manufacturer.Equals(manufacturer, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Get company preferred deck for given depth (VULCRAFT first, then others)
        /// </summary>
        public static StructuralDeck GetPreferredDeck(double ribDepth)
        {
            // Try VULCRAFT first (company standard)
            var vulcraft = GetByManufacturer("VULCRAFT")
                .Where(d => Math.Abs(d.RibDepth - ribDepth) < 0.25)
                .OrderBy(d => Math.Abs(d.RibDepth - ribDepth))
                .FirstOrDefault();

            if (vulcraft != null)
                return vulcraft;

            // Fallback to any manufacturer, closest match by rib depth
            return StandardDecks
                .Where(d => d.Manufacturer != "Generic") // Avoid generic unless no other options
                .OrderBy(d => Math.Abs(d.RibDepth - ribDepth))
                .FirstOrDefault() ?? StandardDecks.Last(); // Generic fallback
        }

        /// <summary>
        /// Find deck by approximate rib depth (within tolerance)
        /// Useful when only thickness is known
        /// </summary>
        public static StructuralDeck FindByDepth(double ribDepth, double tolerance = 0.25)
        {
            return StandardDecks
                .Where(d => Math.Abs(d.RibDepth - ribDepth) <= tolerance && d.Manufacturer != "Generic")
                .OrderBy(d => Math.Abs(d.RibDepth - ribDepth))
                .ThenBy(d => d.Manufacturer == "VULCRAFT" ? 0 : 1) // Prefer VULCRAFT
                .FirstOrDefault();
        }

        /// <summary>
        /// Get all available manufacturers (excluding Generic)
        /// </summary>
        public static List<string> GetManufacturers()
        {
            return StandardDecks
                .Select(d => d.Manufacturer)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(m => m != "Generic")
                .OrderBy(m => m == "VULCRAFT" ? "A" : m) // VULCRAFT first
                .ToList();
        }

        /// <summary>
        /// Calculate match score between this deck and target properties
        /// Lower score = better match
        /// </summary>
        public double CalculateMatchScore(double targetRibDepth, double targetRibSpacing = 0, double targetRibWidthTop = 0)
        {
            double score = Math.Abs(this.RibDepth - targetRibDepth) * 10.0; // Rib depth most important

            if (targetRibSpacing > 0)
            {
                score += Math.Abs(this.RibSpacing - targetRibSpacing) * 2.0;
            }

            if (targetRibWidthTop > 0)
            {
                score += Math.Abs(this.RibWidthTop - targetRibWidthTop) * 1.0;
            }

            // Bonus for company preferred manufacturers
            if (this.Manufacturer == "VULCRAFT")
            {
                score -= 1.0; // Slight preference for VULCRAFT
            }

            return score;
        }

        /// <summary>
        /// Find best matching deck from library based on properties
        /// </summary>
        public static StructuralDeck FindBestMatch(double ribDepth, double ribSpacing = 0, double ribWidthTop = 0)
        {
            var scored = StandardDecks
                .Where(d => d.Manufacturer != "Generic") // Exclude generic from matching
                .Select(deck => new {
                    Deck = deck,
                    Score = deck.CalculateMatchScore(ribDepth, ribSpacing, ribWidthTop)
                })
                .OrderBy(x => x.Score)
                .ToList();

            // Return best match if score is reasonable (threshold can be adjusted)
            var bestMatch = scored.FirstOrDefault();
            if (bestMatch != null && bestMatch.Score < 5.0) // Threshold for acceptable match
            {
                return bestMatch.Deck;
            }

            return null; // No acceptable match found
        }

        #endregion

        #region Instance Methods (for compatibility with existing code)

        /// <summary>
        /// Default constructor
        /// </summary>
        public StructuralDeck() { }

        /// <summary>
        /// Constructor with parameters
        /// </summary>
        public StructuralDeck(string deckType, double ribDepth, double ribWidthTop, double ribSpacing, string manufacturer)
        {
            DeckType = deckType;
            RibDepth = ribDepth;
            RibWidthTop = ribWidthTop;
            RibSpacing = ribSpacing;
            Manufacturer = manufacturer;
        }

        /// <summary>
        /// String representation for debugging
        /// </summary>
        public override string ToString()
        {
            return $"{DeckType} ({Manufacturer}) - Depth: {RibDepth}\", Spacing: {RibSpacing}\"";
        }

        #endregion
    }
}
using Core.Data.Providers;

namespace Core.Models.Properties.Floors
{
    /// <summary>
    /// Properties specific to metal deck floors
    /// </summary>
    public class DeckProperties : FloorTypeProperties
    {
        /// <summary>
        /// Type of metal deck (Composite, NonComposite)
        /// </summary>
        public string DeckType { get; set; }

        /// <summary>
        /// Depth of the metal deck in inches
        /// </summary>
        public double DeckDepth { get; set; }

        /// <summary>
        /// Gage of the metal deck
        /// </summary>
        public int DeckGage { get; set; }

        /// <summary>
        /// Thickness of the concrete topping in inches (for composite decks)
        /// </summary>
        public double ToppingThickness { get; set; }

        /// <summary>
        /// Manufacturer of the metal deck
        /// </summary>
        public string Manufacturer { get; set; }

        /// <summary>
        /// Profile name of the metal deck
        /// </summary>
        public string ProfileName { get; set; }

        /// <summary>
        /// Calculated moment of inertia of the deck profile based on manufacturer data
        /// </summary>
        public double MomentOfInertia => GetProfileData()?.MomentOfInertia ?? 0;

        /// <summary>
        /// Calculated section modulus of the deck profile based on manufacturer data
        /// </summary>
        public double SectionModulus => GetProfileData()?.SectionModulus ?? 0;

        /// <summary>
        /// Weight of the deck in pounds per square foot
        /// </summary>
        public double Weight => GetProfileData()?.Weight ?? 0;

        /// <summary>
        /// Maximum unshored span in feet
        /// </summary>
        public double MaximumUnshoreSpan => GetProfileData()?.MaximumUnshoreSpan ?? 0;

        /// <summary>
        /// Creates a new DeckProperties with default values
        /// </summary>
        public DeckProperties()
        {
            DeckType = "Composite";
            DeckDepth = 1.5;
            DeckGage = 22;
            ToppingThickness = 2.5;
            Manufacturer = "Vulcraft";
            ProfileName = "1.5B";
        }

        /// <summary>
        /// Creates a new DeckProperties with specified values
        /// </summary>
        public DeckProperties(string deckType, double deckDepth, int deckGage)
        {
            DeckType = deckType;
            DeckDepth = deckDepth;
            DeckGage = deckGage;

            if (deckType?.ToLower() == "composite")
            {
                ToppingThickness = 2.5; // Default value
            }
        }

        /// <summary>
        /// Gets profile data from the deck profile provider
        /// </summary>
        private DeckProfile GetProfileData()
        {
            if (string.IsNullOrEmpty(Manufacturer) || string.IsNullOrEmpty(ProfileName) || DeckGage <= 0)
                return null;

            return JsonDeckProvider.GetProfile(Manufacturer, ProfileName, DeckGage);
        }

        /// <summary>
        /// Gets available manufacturers from the deck profile provider
        /// </summary>
        public static string[] GetAvailableManufacturers()
        {
            return JsonDeckProvider.GetAvailableManufacturers();
        }

        /// <summary>
        /// Gets available profile names for a specific manufacturer
        /// </summary>
        public static string[] GetProfilesForManufacturer(string manufacturer)
        {
            return JsonDeckProvider.GetProfilesForManufacturer(manufacturer);
        }
    }
}
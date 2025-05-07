using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Core.Data.Providers
{
    public static class JsonDeckProvider
    {
        private static Dictionary<string, Dictionary<string, List<DeckProfile>>> _profiles;

        static JsonDeckProvider()
        {
            LoadProfileData();
        }

        private static void LoadProfileData()
        {
            _profiles = new Dictionary<string, Dictionary<string, List<DeckProfile>>>();

            try
            {
                // Load from JSON files in a known directory 
                string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Tables", "Decks");
                foreach (string file in Directory.GetFiles(dataPath, "*.json"))
                {
                    string manufacturer = Path.GetFileNameWithoutExtension(file);
                    string json = File.ReadAllText(file);
                    var manufacturerProfiles = JsonSerializer.Deserialize<Dictionary<string, List<DeckProfile>>>(json);
                    _profiles[manufacturer] = manufacturerProfiles;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading deck profile data: {ex.Message}");
                _profiles = new Dictionary<string, Dictionary<string, List<DeckProfile>>>();
            }
        }

        public static DeckProfile GetProfile(string manufacturer, string profileName, int gage)
        {
            if (_profiles.TryGetValue(manufacturer, out var manufacturerProfiles))
                if (manufacturerProfiles.TryGetValue(profileName, out var profiles))
                    return profiles.FirstOrDefault(p => p.Gage == gage);

            return null;
        }

        public static string[] GetAvailableManufacturers() => _profiles.Keys.ToArray();

        public static string[] GetProfilesForManufacturer(string manufacturer)
        {
            if (_profiles.TryGetValue(manufacturer, out var profiles))
                return profiles.Keys.ToArray();
            return Array.Empty<string>();
        }
    }

    public class DeckProfile
    {
        public int Gage { get; set; }
        public double Thickness { get; set; }
        public double MomentOfInertia { get; set; }
        public double SectionModulus { get; set; }
        public double Weight { get; set; }
        public double MaximumUnshoreSpan { get; set; }
        public double MaximumShoreSpan { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models.Properties;
using Core.Utilities;

namespace ETABS.Export.Properties
{
    // Imports wall property definitions from ETABS E2K file
    public class WallPropertiesExport
    {
        // Dictionary to map material names to IDs
        private Dictionary<string, string> _materialIdsByName = new Dictionary<string, string>();

        // Sets the material name to ID mapping for reference when creating wall properties
        public void SetMaterials(IEnumerable<Material> materials)
        {
            _materialIdsByName.Clear();
            foreach (var material in materials)
            {
                if (!string.IsNullOrEmpty(material.Name))
                {
                    _materialIdsByName[material.Name] = material.Id;
                }
            }
        }

        // Imports wall properties from E2K WALL PROPERTIES section
        public List<WallProperties> Export(string wallPropertiesSection)
        {
            var wallProperties = new Dictionary<string, WallProperties>();

            if (string.IsNullOrWhiteSpace(wallPropertiesSection))
                return new List<WallProperties>();

            // Format: SHELLPROP "name" PROPTYPE "Wall" MATERIAL "material" MODELINGTYPE "ShellThin" WALLTHICKNESS thickness
            var propertyPattern = new Regex(@"^\s*SHELLPROP\s+""([^""]+)""\s+PROPTYPE\s+""Wall""\s+MATERIAL\s+""([^""]+)""\s+MODELINGTYPE\s+""([^""]+)""\s+WALLTHICKNESS\s+([\d\.]+)",
                RegexOptions.Multiline);

            // Pattern for modifiers (optional)
            var modifierPattern = new Regex(@"^\s*SHELLPROP\s+""([^""]+)""\s+F11MOD\s+([\d\.]+)\s+F22MOD\s+([\d\.]+)\s+M11MOD\s+([\d\.]+)\s+M22MOD\s+([\d\.]+)",
                RegexOptions.Multiline);

            // Process wall property definitions
            var propertyMatches = propertyPattern.Matches(wallPropertiesSection);
            foreach (Match match in propertyMatches)
            {
                if (match.Groups.Count >= 5)
                {
                    string name = match.Groups[1].Value;
                    string materialName = match.Groups[2].Value;
                    string modelingType = match.Groups[3].Value;
                    double thickness = Convert.ToDouble(match.Groups[4].Value);

                    // Look up material ID
                    string materialId = null;
                    if (_materialIdsByName.TryGetValue(materialName, out string id))
                    {
                        materialId = id;
                    }

                    // Create wall properties
                    var wallProp = new WallProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.WALL_PROPERTIES),
                        Name = name,
                        MaterialId = materialId,
                        Thickness = thickness
                    };

                    // Add optional properties
                    wallProp.Properties["modelingType"] = modelingType;

                    wallProperties[name] = wallProp;
                }
            }

            // Process wall property modifiers
            var modifierMatches = modifierPattern.Matches(wallPropertiesSection);
            foreach (Match match in modifierMatches)
            {
                if (match.Groups.Count >= 6)
                {
                    string name = match.Groups[1].Value;
                    double f11Mod = Convert.ToDouble(match.Groups[2].Value);
                    double f22Mod = Convert.ToDouble(match.Groups[3].Value);
                    double m11Mod = Convert.ToDouble(match.Groups[4].Value);
                    double m22Mod = Convert.ToDouble(match.Groups[5].Value);

                    // Update the wall properties if it exists
                    if (wallProperties.TryGetValue(name, out WallProperties wallProp))
                    {
                        wallProp.Properties["f11Modifier"] = f11Mod;
                        wallProp.Properties["f22Modifier"] = f22Mod;
                        wallProp.Properties["m11Modifier"] = m11Mod;
                        wallProp.Properties["m22Modifier"] = m22Mod;
                    }
                }
            }

            return new List<WallProperties>(wallProperties.Values);
        }
    }
}
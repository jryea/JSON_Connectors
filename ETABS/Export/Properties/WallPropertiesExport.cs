using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Properties;
using Utils = Core.Utilities.Utilities;

namespace ETABS.Export.Properties
{
    // Converts Core WallProperties objects to ETABS E2K format text
    public class WallPropertiesExport
    {
        private IEnumerable<Material> _materials;

       
        // Converts a collection of WallProperties objects to E2K format text
        public string ConvertToE2K(IEnumerable<WallProperties> wallProperties, IEnumerable<Material> materials)
        {
            _materials = materials;
            StringBuilder sb = new StringBuilder();

            // E2K Wall Properties Section Header
            sb.AppendLine("$ WALL PROPERTIES");

            foreach (var wallProp in wallProperties)
            {
                // Format and append each wall property
                string shellProp = FormatWallProperty(wallProp);
                sb.AppendLine(shellProp);

                // Add modifiers on a new line (standard values for stiffness modifiers)
                string modifiers = FormatWallModifiers(wallProp);
                sb.AppendLine(modifiers);
            }

            return sb.ToString();
        }

        // Formats a single WallProperties object as E2K shell property
        private string FormatWallProperty(WallProperties wallProp)
        {
            // Check for null or empty name
            if (string.IsNullOrEmpty(wallProp.Name))
            {
                wallProp.Name = $"{wallProp.Thickness} in wall";
            }

            // Get Wall Material
            string materialName = _materials.FirstOrDefault(m => m.Id == wallProp.MaterialId)?.Name ?? "Unknown";

            // Format: SHELLPROP "name" PROPTYPE "Wall" MATERIAL "material" MODELINGTYPE "ShellThin" WALLTHICKNESS thickness
            return $"  SHELLPROP  \"{wallProp.Name}\"  PROPTYPE  \"Wall\"  MATERIAL \"{materialName}\"  MODELINGTYPE \"ShellThin\"  WALLTHICKNESS {wallProp.Thickness}";
        }

        // Formats wall stiffness modifiers (standard values used in practice)
        private string FormatWallModifiers(WallProperties wallProp)
        {
            // Standard wall modifiers used in practice:
            // F11MOD and F22MOD: 0.5 (in-plane stiffness)
            // M11MOD and M22MOD: 0.01 (out-of-plane bending)

            return $"\tSHELLPROP  \"{wallProp.Name}\"  F11MOD 0.5 F22MOD 0.5 M11MOD 0.01 M22MOD 0.01";
        }

        // Converts a single WallProperties object to E2K format text
        public string ConvertToE2K(WallProperties wallProp)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Wall Properties Section Header
            sb.AppendLine("$ WALL PROPERTIES");

            // Format and append the wall property
            string shellProp = FormatWallProperty(wallProp);
            sb.AppendLine(shellProp);

            // Add modifiers on a new line
            string modifiers = FormatWallModifiers(wallProp);
            sb.AppendLine(modifiers);

            return sb.ToString();
        }
    }
}

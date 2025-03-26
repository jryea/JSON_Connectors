using System.Collections.Generic;
using System.Text;
using Core.Models.Properties;
using Utils = Core.Utilities.Utilities;

namespace ETABS.Export.Properties
{
    /// <summary>
    /// Converts Core WallProperties objects to ETABS E2K format text
    /// </summary>
    public class WallPropertiesExport
    {
        /// <summary>
        /// Converts a collection of WallProperties objects to E2K format text
        /// </summary>
        /// <param name="wallProperties">Collection of WallProperties objects</param>
        /// <returns>E2K format text for wall properties</returns>
        public string ConvertToE2K(IEnumerable<WallProperties> wallProperties)
        {
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

        /// <summary>
        /// Formats a single WallProperties object as E2K shell property
        /// </summary>
        /// <param name="wallProp">Wall properties object</param>
        /// <returns>Formatted E2K shell property string</returns>
        private string FormatWallProperty(WallProperties wallProp)
        {
            // Check for null or empty name
            if (string.IsNullOrEmpty(wallProp.Name))
            {
                wallProp.Name = $"{wallProp.Thickness} in wall";
            }

            // Get Wall Material


            // Format: SHELLPROP "name" PROPTYPE "Wall" MATERIAL "material" MODELINGTYPE "ShellThin" WALLTHICKNESS thickness
            return $"  SHELLPROP  \"{wallProp.Name}\"  PROPTYPE  \"Wall\"  MATERIAL \"{wallProp.MaterialId}\"  MODELINGTYPE \"ShellThin\"  WALLTHICKNESS {wallProp.Thickness}";
        }

        /// <summary>
        /// Formats wall stiffness modifiers (standard values used in practice)
        /// </summary>
        /// <param name="wallProp">Wall properties object</param>
        /// <returns>Formatted E2K wall modifiers string</returns>
        private string FormatWallModifiers(WallProperties wallProp)
        {
            // Standard wall modifiers used in practice:
            // F11MOD and F22MOD: 0.5 (in-plane stiffness)
            // M11MOD and M22MOD: 0.01 (out-of-plane bending)

            return $"\tSHELLPROP  \"{wallProp.Name}\"  F11MOD 0.5 F22MOD 0.5 M11MOD 0.01 M22MOD 0.01";
        }

        /// <summary>
        /// Converts a single WallProperties object to E2K format text
        /// </summary>
        /// <param name="wallProp">WallProperties object</param>
        /// <returns>E2K format text for wall property</returns>
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
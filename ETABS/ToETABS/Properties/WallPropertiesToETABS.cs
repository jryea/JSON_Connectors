using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Properties;

namespace ETABS.ToETABS.Properties
{
    // Converts Core WallProperties objects to ETABS E2K format text
    public class WallPropertiesToETABS
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

            // Replace all "\"" symbols in the name with " inch"
            string formattedName = wallProp.Name.Replace("\"", " inch");

            // Get Wall Material
            string materialName = _materials.FirstOrDefault(m => m.Id == wallProp.MaterialId)?.Name ?? "Concrete";

            // Format according to exact required pattern, no modifiers:
            // SHELLPROP  "IMEG_Concrete 14 3/4""  PROPTYPE  "Wall"  MATERIAL "Concrete"  MODELINGTYPE "ShellThin"  WALLTHICKNESS 14.75
            return $"  SHELLPROP  \"{formattedName}\"  PROPTYPE  \"Wall\"  MATERIAL \"{materialName}\"  MODELINGTYPE \"ShellThin\"  WALLTHICKNESS {wallProp.Thickness}";
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

            return sb.ToString();
        }
    }
}
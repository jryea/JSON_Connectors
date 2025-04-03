using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Properties;

namespace ETABS.Export.Properties
{
    /// <summary>
    /// Converts Core FloorProperties objects to ETABS E2K format text for slab properties
    /// </summary>
    public class FloorPropertiesToETABS
    {
        private IEnumerable<Material> _materials;

        /// <summary>
        /// Converts a collection of FloorProperties objects to E2K format text
        /// </summary>
        /// <param name="floorProperties">Collection of FloorProperties objects</param>
        /// <param name="materials">Collection of Material objects for reference</param>
        /// <returns>E2K format text for slab properties</returns>
        public string ConvertToE2K(IEnumerable<FloorProperties> floorProperties, IEnumerable<Material> materials)
        {
            _materials = materials;
            StringBuilder sb = new StringBuilder();

            // E2K Slab Properties Section Header
            sb.AppendLine("$ SLAB PROPERTIES");

            if (floorProperties == null || !floorProperties.Any())
            {
                // Add a default slab property
                sb.AppendLine("  SHELLPROP  \"Slab1\"  PROPTYPE  \"Slab\"  MATERIAL \"4000 psi\"  MODELINGTYPE \"ShellThin\"  SLABTYPE \"Slab\"  SLABTHICKNESS 8");
                return sb.ToString();
            }

            // Process each floor property as a slab
            foreach (var floorProp in floorProperties)
            {
                // Format and append each slab property
                string slabPropertyLine = FormatSlabProperty(floorProp);
                sb.AppendLine(slabPropertyLine);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a single FloorProperties object as E2K shell property
        /// </summary>
        private string FormatSlabProperty(FloorProperties floorProp)
        {
            // Check for null or empty name
            if (string.IsNullOrEmpty(floorProp.Name))
            {
                floorProp.Name = $"{floorProp.Thickness} in slab";
            }

            // Get Material
            string materialName = _materials.FirstOrDefault(m => m.Id == floorProp.MaterialId)?.Name ?? "4000 psi";

            // Determine modeling type (default to ShellThin)
            string modelingType = "ShellThin";
            if (floorProp.SlabProperties != null &&
                floorProp.SlabProperties.ContainsKey("modelingType") &&
                floorProp.SlabProperties["modelingType"] is string specifiedType)
            {
                modelingType = specifiedType;
            }

            // Determine slab type (default to "Slab")
            string slabType = "Slab";
            if (floorProp.Type?.ToLower() == "waffle")
                slabType = "Waffle";
            else if (floorProp.Type?.ToLower() == "ribbed")
                slabType = "Ribbed";

            // Format: SHELLPROP "Slab1" PROPTYPE "Slab" MATERIAL "4000 psi" MODELINGTYPE "ShellThin" SLABTYPE "Slab" SLABTHICKNESS 8
            return $"  SHELLPROP  \"{floorProp.Name}\"  PROPTYPE  \"Slab\"  MATERIAL \"{materialName}\"  " +
                   $"MODELINGTYPE \"{modelingType}\"  SLABTYPE \"{slabType}\"  SLABTHICKNESS {floorProp.Thickness}";
        }

        /// <summary>
        /// Checks if floor properties include deck-specific information
        /// </summary>
        private bool IsDeckProperty(FloorProperties floorProp)
        {
            if (string.IsNullOrEmpty(floorProp.Type))
                return false;

            return floorProp.Type.ToLower() == "composite" ||
                   floorProp.Type.ToLower() == "noncomposite" ||
                   floorProp.Type.ToLower() == "deck";
        }

        /// <summary>
        /// Converts a single FloorProperties object to E2K format text
        /// </summary>
        public string ConvertToE2K(FloorProperties floorProp)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Slab Properties Section Header
            sb.AppendLine("$ SLAB PROPERTIES");

            // Format and append the slab property
            string slabPropertyLine = FormatSlabProperty(floorProp);
            sb.AppendLine(slabPropertyLine);

            return sb.ToString();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Properties;

namespace ETABS.Import.Properties
{
    // Converts Core FrameProperties objects to ETABS E2K format
    public class FramePropertiesImport
    {
        private IEnumerable<Material> _materials;

        // Converts a collection of FrameProperties objects to E2K format text.
        public string ConvertToE2K(
            IEnumerable<FrameProperties> frameProperties,
            IEnumerable<Material> materials)
        {
            _materials = materials;
            StringBuilder sb = new StringBuilder();

            // E2K Frame Sections Header
            sb.AppendLine("$ FRAME SECTIONS");
            foreach (var frameProp in frameProperties)
            {
                // Format and append each frame property definition
                string sectionDefinition = FormatFrameSection(frameProp);
                sb.AppendLine(sectionDefinition);
            }
            return sb.ToString();
        }

        // Formats a single FrameProperties object as E2K frame section.
        private string FormatFrameSection(FrameProperties frameProp)
        {
            // Check for null or empty name
            if (string.IsNullOrEmpty(frameProp.Name))
            {
                frameProp.Name = $"{frameProp.Shape ?? "CUSTOM"}_SECTION";
            }

            // Find the material name
            string materialName = _materials.FirstOrDefault(m => m.Id == frameProp.MaterialId)?.Name ?? "Unknown";

            // Format depends on shape type
            switch (frameProp.Shape?.ToUpper())
            {
                case "HSS":
                case "TUBE":
                    return FormatHSS(frameProp, materialName);

                // Other shape-specific formatters...
                default:
                    return FormatGeneric(frameProp, materialName);
            }
        }

        // Formats a Hollow Structural Section (HSS) or Tube section
        private string FormatHSS(FrameProperties frameProp, string materialName)
        {
            // Get dimensions with defaults
            double depth = GetDimension(frameProp, "depth", 6.0);
            double width = GetDimension(frameProp, "width", 6.0);
            double thickness = GetDimension(frameProp, "wallThickness", 0.25);

            // Format: FRAMESECTION "HSS6X6X0.25" MATERIAL "A992Fy50" SHAPE "HSS/Tube" D 6 B 6 T 0.25
            return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"HSS/Tube\"  D {depth} B {width} T {thickness}";
        }

        // Formats a generic section (for unrecognized shapes)
        private string FormatGeneric(FrameProperties frameProp, string materialName)
        {
            string shape = !string.IsNullOrEmpty(frameProp.Shape) ? frameProp.Shape : "Custom";
            return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"{frameProp.Name}\"";
        }

        // Gets a dimension value from the FrameProperties dimensions dictionary with a default fallback
        private double GetDimension(FrameProperties frameProp, string dimensionName, double defaultValue)
        {
            if (frameProp.Dimensions != null && frameProp.Dimensions.ContainsKey(dimensionName))
            {
                return frameProp.Dimensions[dimensionName];
            }
            return defaultValue;
        }
    }
}

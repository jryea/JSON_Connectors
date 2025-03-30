using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Properties;

namespace ETABS.Export.Properties
{
    // Converts Core FrameProperties objects to ETABS E2K format
    public class FramePropertiesExport
    {
        private IEnumerable<Material> _materials;


        // Converts a collection of FrameProperties objects to E2K format text
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


        // Formats a single FrameProperties object as E2K frame section
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
                case "W":
                case "STEEL I/WIDE FLANGE":
                    return FormatWideFlange(frameProp, materialName);

                case "HSS":
                case "TUBE":
                    return FormatHSS(frameProp, materialName);

                case "PIPE":
                    return FormatPipe(frameProp, materialName);

                case "C":
                case "CHANNEL":
                    return FormatChannel(frameProp, materialName);

                case "L":
                case "ANGLE":
                    return FormatAngle(frameProp, materialName);

                case "RECT":
                case "RECTANGULAR":
                    return FormatRectangular(frameProp, materialName);

                case "CIRCLE":
                case "CIRCULAR":
                    return FormatCircular(frameProp, materialName);

                default:
                    // Generic format for custom or unrecognized shapes
                    return FormatGeneric(frameProp, materialName);
            }
        }

        #region Shape-Specific Formatters

        // Formats a Wide Flange (W) section
        private string FormatWideFlange(FrameProperties frameProp, string materialName)
        {
            // Check if this is likely a standard section (like W12X26)
            if (frameProp.Name.StartsWith("W") && frameProp.Name.Contains("X"))
            {
                // If it's a standard section, use simpler format
                // Format: FRAMESECTION "W12X26" MATERIAL "A992Fy50" SHAPE "W12X26"
                return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"{frameProp.Name}\"";
            }

            // For non-standard sections, include dimensions
            // Get dimensions with defaults
            double depth = GetDimension(frameProp, "depth", 12.0);
            double width = GetDimension(frameProp, "width", 6.0);
            double tf = GetDimension(frameProp, "flangeThickness", 0.5);
            double tw = GetDimension(frameProp, "webThickness", 0.25);

            // Format: FRAMESECTION "SteelBm" MATERIAL "A992Fy50" SHAPE "Steel I/Wide Flange" D 18 B 6 TF 0.5 TW 0.25
            return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"Steel I/Wide Flange\"  D {depth} B {width} TF {tf} TW {tw}";
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

        // Formats a Pipe section
        private string FormatPipe(FrameProperties frameProp, string materialName)
        {
            // Get dimensions with defaults
            double diameter = GetDimension(frameProp, "outerDiameter", 6.0);
            double thickness = GetDimension(frameProp, "wallThickness", 0.25);

            // Format: FRAMESECTION "PIPE6STD" MATERIAL "A992Fy50" SHAPE "Pipe" OD 6 T 0.25
            return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"Pipe\"  OD {diameter} T {thickness}";
        }

        // Formats a Channel (C) section
        private string FormatChannel(FrameProperties frameProp, string materialName)
        {
            // Get dimensions with defaults
            double depth = GetDimension(frameProp, "depth", 10.0);
            double width = GetDimension(frameProp, "width", 3.0);
            double tf = GetDimension(frameProp, "flangeThickness", 0.4);
            double tw = GetDimension(frameProp, "webThickness", 0.3);

            // Format: FRAMESECTION "C10X20" MATERIAL "A992Fy50" SHAPE "Channel" D 10 B 3 TF 0.4 TW 0.3
            return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"Channel\"  D {depth} B {width} TF {tf} TW {tw}";
        }

        // Formats an Angle (L) section
        private string FormatAngle(FrameProperties frameProp, string materialName)
        {
            // Get dimensions with defaults
            double depth = GetDimension(frameProp, "depth", 4.0);
            double width = GetDimension(frameProp, "width", 4.0);
            double thickness = GetDimension(frameProp, "thickness", 0.375);

            // Format: FRAMESECTION "L4X4X3/8" MATERIAL "A992Fy50" SHAPE "Angle" D 4 B 4 T 0.375
            return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"Angle\"  D {depth} B {width} T {thickness}";
        }

        // Formats a Rectangular section (often used for concrete)
        private string FormatRectangular(FrameProperties frameProp, string materialName)
        {
            // Get dimensions with defaults
            double depth = GetDimension(frameProp, "depth", 18.0);
            double width = GetDimension(frameProp, "width", 18.0);

            // Format: FRAMESECTION "18X18" MATERIAL "4000Psi" SHAPE "Concrete Rectangular" D 18 B 18
            return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"Concrete Rectangular\"  D {depth} B {width}";
        }

        // Formats a Circular section (often used for concrete)
        private string FormatCircular(FrameProperties frameProp, string materialName)
        {
            // Get dimensions with defaults
            double diameter = GetDimension(frameProp, "diameter", 16.0);

            // Format: FRAMESECTION "CIRC16" MATERIAL "4000Psi" SHAPE "Concrete Circle" D 16
            return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"Concrete Circle\"  D {diameter}";
        }

        // Formats a generic section (for unrecognized shapes)
        private string FormatGeneric(FrameProperties frameProp, string materialName)
        {
            // For generic or custom shapes, use the shape field directly if it exists
            string shape = !string.IsNullOrEmpty(frameProp.Shape) ? frameProp.Shape : "Custom";

            // Format: FRAMESECTION "Custom1" MATERIAL "Steel" SHAPE "Custom"
            return $"  FRAMESECTION  \"{frameProp.Name}\"  MATERIAL \"{materialName}\"  SHAPE \"{shape}\"";
        }

        #endregion

        // Gets a dimension value from the FrameProperties dimensions dictionary with a default fallback
        private double GetDimension(FrameProperties frameProp, string dimensionName, double defaultValue)
        {
            if (frameProp.Dimensions != null && frameProp.Dimensions.ContainsKey(dimensionName))
            {
                return frameProp.Dimensions[dimensionName];
            }
            return defaultValue;
        }

        // Converts a single FrameProperties object to E2K format text
        public string ConvertToE2K(FrameProperties frameProp)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Frame Sections Header
            sb.AppendLine("$ FRAME SECTIONS");

            // Format and append the frame section
            string sectionDefinition = FormatFrameSection(frameProp);
            sb.AppendLine(sectionDefinition);

            return sb.ToString();
        }
    }
}
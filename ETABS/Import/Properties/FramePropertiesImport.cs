using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models;
using Core.Models.Properties;

namespace ETABS.Import.Properties
{
    // Converts Core FrameProperties objects to ETABS E2K format text
    public class FramePropertiesImport
    {
        private IEnumerable<Material> _materials;

        // Converts a collection of FrameProperties objects to E2K format text
        public string ConvertToE2K(IEnumerable<FrameProperties> frameProperties, IEnumerable<Material> materials)
        {
            _materials = materials;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("$ FRAME SECTIONS");

            if (frameProperties == null || !frameProperties.Any())
            {
                sb.AppendLine("  FRAMESECTION  \"W12X26\"  MATERIAL \"A992Fy50\"  SHAPE \"W12X26\"  D 12 B 6.5 TF 0.525 TW 0.38");
                return sb.ToString();
            }

            foreach (var frameProp in frameProperties)
            {
                string sectionDefinition = FormatFrameSection(frameProp);
                sb.AppendLine(sectionDefinition);

                // Add modifiers if they are not all 1.0
                string modifiersLine = FormatFrameModifiers(frameProp);
                if (!string.IsNullOrEmpty(modifiersLine))
                {
                    sb.AppendLine(modifiersLine);
                }
            }

            return sb.ToString();
        }

        private string FormatFrameSection(FrameProperties frameProp)
        {
            string formattedName = frameProp.Name.Replace("\u0022", " inch");
            string materialName = _materials.FirstOrDefault(m => m.Id == frameProp.MaterialId)?.Name ?? "Unknown";
            materialName = materialName.Replace("\u0022", " inch");

            if (frameProp.Type == FrameMaterialType.Steel && frameProp.SteelProps != null)
            {
                return FormatSteelSection(frameProp.SteelProps, materialName, formattedName);
            }
            else if (frameProp.Type == FrameMaterialType.Concrete && frameProp.ConcreteProps != null)
            {
                return FormatConcreteSection(frameProp.ConcreteProps, materialName, formattedName);
            }
            else
            {
                return $"  FRAMESECTION  \"{formattedName}\"  MATERIAL \"{materialName}\"  SHAPE \"{formattedName}\"";
            }
        }

        private string FormatSteelSection(SteelFrameProperties steelProps, string materialName, string formattedName)
        {
            string sectionName = !string.IsNullOrEmpty(steelProps.SectionName) ? steelProps.SectionName : formattedName;

            // For steel sections, use the section name directly since dimensions are standard
            return $"  FRAMESECTION  \"{formattedName}\"  MATERIAL \"{materialName}\"  SHAPE \"{sectionName}\"";
        }

        private string FormatConcreteSection(ConcreteFrameProperties concreteProps, string materialName, string formattedName)
        {
            string shapeType = GetConcreteShapeType(concreteProps.SectionType);

            // Base format
            StringBuilder sb = new StringBuilder($"  FRAMESECTION  \"{formattedName}\"  MATERIAL \"{materialName}\"  SHAPE \"{shapeType}\"");

            // Add dimensions based on section type
            switch (concreteProps.SectionType)
            {
                case ConcreteSectionType.Rectangular:
                    // Use depth and width properties directly, with fallback to Dimensions dictionary
                    double depth = GetDimensionValue(concreteProps, "depth", 24);
                    double width = GetDimensionValue(concreteProps, "width", 12);
                    sb.Append($"  D {depth}  B {width}");
                    break;

                case ConcreteSectionType.Circular:
                    double diameter = GetDimensionValue(concreteProps, "diameter", 24);
                    sb.Append($"  D {diameter}");
                    break;

                case ConcreteSectionType.TShaped:
                    depth = GetDimensionValue(concreteProps, "depth", 24);
                    width = GetDimensionValue(concreteProps, "width", 18);
                    double flangeThickness = GetDimensionValue(concreteProps, "flangeThickness", 8);
                    double webThickness = GetDimensionValue(concreteProps, "webThickness", 10);
                    sb.Append($"  D {depth}  B {width}  TF {flangeThickness}  TW {webThickness}");
                    break;

                case ConcreteSectionType.LShaped:
                    depth = GetDimensionValue(concreteProps, "depth", 24);
                    width = GetDimensionValue(concreteProps, "width", 24);
                    double thickness1 = GetDimensionValue(concreteProps, "thickness1", 12);
                    double thickness2 = GetDimensionValue(concreteProps, "thickness2", 12);
                    sb.Append($"  D {depth}  B {width}  T1 {thickness1}  T2 {thickness2}");
                    break;
            }

            return sb.ToString();
        }

        private double GetDimensionValue(ConcreteFrameProperties concreteProps, string dimensionName, double defaultValue)
        {
            // First try to get from direct properties (for JSON deserialization)
            switch (dimensionName.ToLower())
            {
                case "depth":
                    if (concreteProps.Depth > 0) return concreteProps.Depth;
                    break;
                case "width":
                    if (concreteProps.Width > 0) return concreteProps.Width;
                    break;
            }

            // Then try the Dimensions dictionary (for backward compatibility)
            if (concreteProps.Dimensions != null &&
                concreteProps.Dimensions.TryGetValue(dimensionName, out string dimValue) &&
                double.TryParse(dimValue, out double result))
            {
                return result;
            }

            // Return default value
            return defaultValue;
        }

        private string FormatFrameModifiers(FrameProperties frameProp)
        {
            if (frameProp.ETABSModifiers == null)
                return string.Empty;

            var modifiers = frameProp.ETABSModifiers;
            var modifierParts = new List<string>();

            // Check each modifier and add to list if not 1.0
            if (Math.Abs(modifiers.Area - 1.0) > 1e-6)
                modifierParts.Add($"AMOD {modifiers.Area}");

            if (Math.Abs(modifiers.A22 - 1.0) > 1e-6)
                modifierParts.Add($"A2MOD {modifiers.A22}");

            if (Math.Abs(modifiers.A33 - 1.0) > 1e-6)
                modifierParts.Add($"A3MOD {modifiers.A33}");

            if (Math.Abs(modifiers.Torsion - 1.0) > 1e-6)
                modifierParts.Add($"JMOD {modifiers.Torsion}");

            if (Math.Abs(modifiers.I22 - 1.0) > 1e-6)
                modifierParts.Add($"I2MOD {modifiers.I22}");

            if (Math.Abs(modifiers.I33 - 1.0) > 1e-6)
                modifierParts.Add($"I3MOD {modifiers.I33}");

            if (Math.Abs(modifiers.Mass - 1.0) > 1e-6)
                modifierParts.Add($"MMOD {modifiers.Mass}");

            if (Math.Abs(modifiers.Weight - 1.0) > 1e-6)
                modifierParts.Add($"WMOD {modifiers.Weight}");

            // If no modifiers are different from 1.0, return empty
            if (modifierParts.Count == 0)
                return string.Empty;

            // Format the modifiers line
            return $"  FRAMESECTION  \"{frameProp.Name.Replace("\u0022", " inch")}\"  {string.Join(" ", modifierParts)}";
        }

        private string GetConcreteShapeType(ConcreteSectionType sectionType)
        {
            switch (sectionType)
            {
                case ConcreteSectionType.Rectangular:
                    return "Concrete Rectangular";
                case ConcreteSectionType.Circular:
                    return "Concrete Circle";
                case ConcreteSectionType.TShaped:
                    return "Concrete Tee";
                case ConcreteSectionType.LShaped:
                    return "Concrete L-Section";
                default:
                    return "Concrete";
            }
        }

        // Converts a single FrameProperties object to E2K format text
        public string ConvertToE2K(FrameProperties frameProp)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("$ FRAME SECTIONS");

            string sectionDefinition = FormatFrameSection(frameProp);
            sb.AppendLine(sectionDefinition);

            // Add modifiers if they are not all 1.0
            string modifiersLine = FormatFrameModifiers(frameProp);
            if (!string.IsNullOrEmpty(modifiersLine))
            {
                sb.AppendLine(modifiersLine);
            }

            return sb.ToString();
        }
    }
}
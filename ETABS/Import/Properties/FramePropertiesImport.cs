using Core.Models;
using Core.Models.Properties;
using Core.Utilities;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System;

public class FramePropertiesImport
{
    private IEnumerable<Material> _materials;

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
        string sectionType = steelProps.SectionType.ToString();

        // For steel sections, use the section name directly since dimensions are implied
        return $"  FRAMESECTION  \"{formattedName}\"  MATERIAL \"{materialName}\"  SHAPE \"{sectionName}\"";
    }

    private string FormatConcreteSection(ConcreteFrameProperties concreteProps, string materialName, string formattedName)
    {
        string sectionName = !string.IsNullOrEmpty(concreteProps.SectionName) ? concreteProps.SectionName : formattedName;
        string shapeType = GetConcreteShapeType(concreteProps.SectionType);

        // Base format
        StringBuilder sb = new StringBuilder($"  FRAMESECTION  \"{formattedName}\"  MATERIAL \"{materialName}\"  SHAPE \"{shapeType}\"");

        // Add dimensions from the string dictionary
        switch (concreteProps.SectionType)
        {
            case ConcreteSectionType.Rectangular:
                AppendDimension(sb, concreteProps.Dimensions, "depth", "D", "24");
                AppendDimension(sb, concreteProps.Dimensions, "width", "B", "12");
                break;

            case ConcreteSectionType.Circular:
                AppendDimension(sb, concreteProps.Dimensions, "diameter", "D", "24");
                break;

            case ConcreteSectionType.TShaped:
                AppendDimension(sb, concreteProps.Dimensions, "depth", "D", "24");
                AppendDimension(sb, concreteProps.Dimensions, "width", "B", "18");
                AppendDimension(sb, concreteProps.Dimensions, "flangeThickness", "TF", "8");
                AppendDimension(sb, concreteProps.Dimensions, "webThickness", "TW", "10");
                break;

            case ConcreteSectionType.LShaped:
                AppendDimension(sb, concreteProps.Dimensions, "depth", "D", "24");
                AppendDimension(sb, concreteProps.Dimensions, "width", "B", "24");
                AppendDimension(sb, concreteProps.Dimensions, "thickness1", "T1", "12");
                AppendDimension(sb, concreteProps.Dimensions, "thickness2", "T2", "12");
                break;
        }

        return sb.ToString();
    }

    private void AppendDimension(StringBuilder sb, Dictionary<string, string> dimensions,
                                string dictKey, string etabsParam, string defaultValue)
    {
        string value = dimensions.TryGetValue(dictKey, out string dimValue) ? dimValue : defaultValue;
        sb.Append($"  {etabsParam} {value}");
    }

    private string GetConcreteShapeType(ConcreteSectionType sectionType)
    {
        switch (sectionType)
        {
            case ConcreteSectionType.Rectangular:
                return "Rectangular";
            case ConcreteSectionType.Circular:
                return "Circle";
            case ConcreteSectionType.TShaped:
                return "Tee";
            case ConcreteSectionType.LShaped:
                return "L-Section";
            default:
                return "Concrete";
        }
    }
}

public class FramePropertiesExport
{
    private Dictionary<string, string> _materialIdsByName = new Dictionary<string, string>();

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

    public List<FrameProperties> Export(string frameSectionsSection)
    {
        var frameProperties = new Dictionary<string, FrameProperties>();

        if (string.IsNullOrWhiteSpace(frameSectionsSection))
            return new List<FrameProperties>();

        // Regular expression to match frame section definition
        // Format: FRAMESECTION "W12X26" MATERIAL "A992Fy50" SHAPE "W12X26"
        var basicPattern = new Regex(@"^\s*FRAMESECTION\s+""([^""]+)""\s+MATERIAL\s+""([^""]+)""\s+SHAPE\s+""([^""]+)""",
            RegexOptions.Multiline);

        // Get all matches
        var basicMatches = basicPattern.Matches(frameSectionsSection);

        // Process sections matches...
        foreach (Match match in basicMatches)
        {
            if (match.Groups.Count >= 4)
            {
                string name = match.Groups[1].Value;
                string materialName = match.Groups[2].Value;
                string shape = match.Groups[3].Value;

                // Look up material ID
                string materialId = null;
                if (_materialIdsByName.TryGetValue(materialName, out string id))
                {
                    materialId = id;
                }

                // Determine whether it's steel or concrete based on shape or material
                FrameMaterialType materialType = DetermineMaterialType(shape, materialName);

                // Create frame properties
                var frameProp = new FrameProperties
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                    Name = name,
                    MaterialId = materialId,
                    Type = materialType
                };

                // Initialize appropriate properties
                if (materialType == FrameMaterialType.Steel)
                {
                    frameProp.SteelProps = new SteelFrameProperties
                    {
                        SectionType = DetermineSteelSectionType(shape),
                        SectionName = shape
                    };
                }
                else
                {
                    frameProp.ConcreteProps = new ConcreteFrameProperties
                    {
                        SectionType = DetermineConcreteSectionType(shape),
                        SectionName = shape,
                        Dimensions = new Dictionary<string, string>()
                    };

                    // Parse dimensions
                    var dimMatches = ExtractDimensions(match.Value);
                    foreach (var dimMatch in dimMatches)
                    {
                        string dimName = GetDimensionName(dimMatch.Key);
                        frameProp.ConcreteProps.Dimensions[dimName] = dimMatch.Value;
                    }
                }

                frameProperties[name] = frameProp;
            }
        }

        return new List<FrameProperties>(frameProperties.Values);
    }

    private FrameMaterialType DetermineMaterialType(string shape, string materialName)
    {
        // Look for steel keywords
        if (shape.StartsWith("W") || shape.StartsWith("HSS") || shape.StartsWith("PIPE") ||
            shape.StartsWith("C") || shape.StartsWith("L") || shape.Contains("Steel") ||
            materialName.Contains("Steel") || materialName.Contains("A992"))
        {
            return FrameMaterialType.Steel;
        }

        // Otherwise assume concrete
        return FrameMaterialType.Concrete;
    }

    private SteelSectionType DetermineSteelSectionType(string shape)
    {
        if (shape.StartsWith("W", StringComparison.OrdinalIgnoreCase))
            return SteelSectionType.W;
        else if (shape.StartsWith("HSS", StringComparison.OrdinalIgnoreCase))
            return SteelSectionType.HSS;
        else if (shape.StartsWith("PIPE", StringComparison.OrdinalIgnoreCase))
            return SteelSectionType.PIPE;
        else if (shape.StartsWith("C", StringComparison.OrdinalIgnoreCase))
            return SteelSectionType.C;
        else if (shape.StartsWith("L", StringComparison.OrdinalIgnoreCase))
            return SteelSectionType.L;
        else if (shape.StartsWith("WT", StringComparison.OrdinalIgnoreCase))
            return SteelSectionType.WT;
        else if (shape.StartsWith("ST", StringComparison.OrdinalIgnoreCase))
            return SteelSectionType.ST;
        else if (shape.StartsWith("MC", StringComparison.OrdinalIgnoreCase))
            return SteelSectionType.MC;
        else if (shape.StartsWith("HP", StringComparison.OrdinalIgnoreCase))
            return SteelSectionType.HP;
        else
            return SteelSectionType.W; // Default
    }

    private ConcreteSectionType DetermineConcreteSectionType(string shape)
    {
        if (shape.Contains("Rectangular"))
            return ConcreteSectionType.Rectangular;
        else if (shape.Contains("Circle") || shape.Contains("Circular"))
            return ConcreteSectionType.Circular;
        else if (shape.Contains("Tee") || shape.Contains("T-Shaped"))
            return ConcreteSectionType.TShaped;
        else if (shape.Contains("L-Section") || shape.Contains("L-Shaped"))
            return ConcreteSectionType.LShaped;
        else
            return ConcreteSectionType.Custom;
    }

    private Dictionary<string, string> ExtractDimensions(string sectionText)
    {
        var dimensions = new Dictionary<string, string>();

        // Common dimension parameters
        string[] paramNames = new[] { "D", "B", "TF", "TW", "T", "T1", "T2", "OD" };

        foreach (string param in paramNames)
        {
            var match = Regex.Match(sectionText, $@"{param}\s+([\d\.]+)");
            if (match.Success && match.Groups.Count >= 2)
            {
                dimensions[param] = match.Groups[1].Value;
            }
        }

        return dimensions;
    }

    private string GetDimensionName(string etabsParam)
    {
        switch (etabsParam)
        {
            case "D": return "depth";
            case "B": return "width";
            case "TF": return "flangeThickness";
            case "TW": return "webThickness";
            case "T": return "thickness";
            case "T1": return "thickness1";
            case "T2": return "thickness2";
            case "OD": return "diameter";
            default: return etabsParam.ToLower();
        }
    }
}
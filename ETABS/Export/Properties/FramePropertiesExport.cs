using Core.Models;
using Core.Models.Properties;
using Core.Utilities;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using System.Linq;

namespace ETABS.Export.Properties
{

    public class FramePropertiesExport
    {
        // Dictionary to map material names to IDs
        private Dictionary<string, string> _materialIdsByName = new Dictionary<string, string>();

        // Materials collection for reference
        private IEnumerable<Material> _materials;

        public void SetMaterials(IEnumerable<Material> materials)
        {
            _materials = materials; // Store the materials collection
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
            var basicPattern = new Regex(@"^\s*FRAMESECTION\s+""([^""]+)""\s+MATERIAL\s+""([^""]+)""\s+SHAPE\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Get all matches
            var basicMatches = basicPattern.Matches(frameSectionsSection);

            // Process each match
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
            // Check if we can find the material in our collection
            if (_materials != null)
            {
                var material = _materials.FirstOrDefault(m => m.Name == materialName);
                if (material != null)
                {
                    return material.Type == MaterialType.Steel ?
                        FrameMaterialType.Steel :
                        FrameMaterialType.Concrete;
                }
            }

            // Fallback: Look for steel keywords
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
}
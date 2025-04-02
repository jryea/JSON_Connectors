using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models.Properties;
using Core.Utilities;

namespace ETABS.Import.Properties
{
    // Imports frame section properties from ETABS E2K file
    public class FramePropertiesImport
    {
        // Dictionary to map material names to IDs
        private Dictionary<string, string> _materialIdsByName = new Dictionary<string, string>();

        // Sets the material name to ID mapping for reference when creating frame properties
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

        // Imports frame properties from E2K FRAME SECTIONS section
        public List<FrameProperties> Import(string frameSectionsSection)
        {
            var frameProperties = new Dictionary<string, FrameProperties>();

            if (string.IsNullOrWhiteSpace(frameSectionsSection))
                return new List<FrameProperties>();

            // Regular expression to match frame section definition
            // Format: FRAMESECTION "W12X26" MATERIAL "A992Fy50" SHAPE "W12X26"
            var basicPattern = new Regex(@"^\s*FRAMESECTION\s+""([^""]+)""\s+MATERIAL\s+""([^""]+)""\s+SHAPE\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Pattern for I-section properties
            var iSectionPattern = new Regex(@"^\s*FRAMESECTION\s+""([^""]+)""\s+MATERIAL\s+""([^""]+)""\s+SHAPE\s+""([^""]+)""\s+D\s+([\d\.]+)\s+B\s+([\d\.]+)\s+TF\s+([\d\.]+)\s+TW\s+([\d\.]+)",
                RegexOptions.Multiline);

            // Pattern for HSS/tube properties
            var hssPattern = new Regex(@"^\s*FRAMESECTION\s+""([^""]+)""\s+MATERIAL\s+""([^""]+)""\s+SHAPE\s+""([^""]+)""\s+D\s+([\d\.]+)\s+B\s+([\d\.]+)\s+T\s+([\d\.]+)",
                RegexOptions.Multiline);

            // Pattern for pipe properties
            var pipePattern = new Regex(@"^\s*FRAMESECTION\s+""([^""]+)""\s+MATERIAL\s+""([^""]+)""\s+SHAPE\s+""([^""]+)""\s+OD\s+([\d\.]+)\s+T\s+([\d\.]+)",
                RegexOptions.Multiline);

            // Pattern for rectangular properties
            var rectPattern = new Regex(@"^\s*FRAMESECTION\s+""([^""]+)""\s+MATERIAL\s+""([^""]+)""\s+SHAPE\s+""([^""]+)""\s+D\s+([\d\.]+)\s+B\s+([\d\.]+)",
                RegexOptions.Multiline);

            // Process basic frame section definitions first
            var basicMatches = basicPattern.Matches(frameSectionsSection);
            foreach (Match match in basicMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string name = match.Groups[1].Value;
                    string materialName = match.Groups[2].Value;
                    string shape = match.Groups[3].Value;

                    // Skip if already processed with more detailed pattern
                    if (frameProperties.ContainsKey(name))
                        continue;

                    // Look up material ID
                    string materialId = null;
                    if (_materialIdsByName.TryGetValue(materialName, out string id))
                    {
                        materialId = id;
                    }

                    // Create frame properties
                    var frameProp = new FrameProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                        Name = name,
                        MaterialId = materialId,
                        Shape = shape
                    };

                    // Extract shape type (W, HSS, etc.) from the shape name
                    string shapeType = ExtractShapeType(shape);
                    if (!string.IsNullOrEmpty(shapeType))
                    {
                        frameProp.Shape = shapeType;
                    }

                    frameProperties[name] = frameProp;
                }
            }

            // Process I-section properties
            var iSectionMatches = iSectionPattern.Matches(frameSectionsSection);
            foreach (Match match in iSectionMatches)
            {
                if (match.Groups.Count >= 8)
                {
                    string name = match.Groups[1].Value;
                    string materialName = match.Groups[2].Value;
                    string shape = match.Groups[3].Value;
                    double depth = Convert.ToDouble(match.Groups[4].Value);
                    double width = Convert.ToDouble(match.Groups[5].Value);
                    double tf = Convert.ToDouble(match.Groups[6].Value);
                    double tw = Convert.ToDouble(match.Groups[7].Value);

                    // Look up material ID
                    string materialId = null;
                    if (_materialIdsByName.TryGetValue(materialName, out string id))
                    {
                        materialId = id;
                    }

                    // Create frame properties
                    var frameProp = new FrameProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                        Name = name,
                        MaterialId = materialId,
                        Shape = ExtractShapeType(shape)
                    };

                    // Set dimensions
                    frameProp.Dimensions["depth"] = depth;
                    frameProp.Dimensions["width"] = width;
                    frameProp.Dimensions["flangeThickness"] = tf;
                    frameProp.Dimensions["webThickness"] = tw;

                    frameProperties[name] = frameProp;
                }
            }

            // Process HSS/tube properties
            var hssMatches = hssPattern.Matches(frameSectionsSection);
            foreach (Match match in hssMatches)
            {
                if (match.Groups.Count >= 7)
                {
                    string name = match.Groups[1].Value;
                    string materialName = match.Groups[2].Value;
                    string shape = match.Groups[3].Value;
                    double depth = Convert.ToDouble(match.Groups[4].Value);
                    double width = Convert.ToDouble(match.Groups[5].Value);
                    double thickness = Convert.ToDouble(match.Groups[6].Value);

                    // Look up material ID
                    string materialId = null;
                    if (_materialIdsByName.TryGetValue(materialName, out string id))
                    {
                        materialId = id;
                    }

                    // Create frame properties
                    var frameProp = new FrameProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                        Name = name,
                        MaterialId = materialId,
                        Shape = "HSS"
                    };

                    // Set dimensions
                    frameProp.Dimensions["depth"] = depth;
                    frameProp.Dimensions["width"] = width;
                    frameProp.Dimensions["wallThickness"] = thickness;

                    frameProperties[name] = frameProp;
                }
            }

            // Process pipe properties
            var pipeMatches = pipePattern.Matches(frameSectionsSection);
            foreach (Match match in pipeMatches)
            {
                if (match.Groups.Count >= 6)
                {
                    string name = match.Groups[1].Value;
                    string materialName = match.Groups[2].Value;
                    string shape = match.Groups[3].Value;
                    double od = Convert.ToDouble(match.Groups[4].Value);
                    double thickness = Convert.ToDouble(match.Groups[5].Value);

                    // Look up material ID
                    string materialId = null;
                    if (_materialIdsByName.TryGetValue(materialName, out string id))
                    {
                        materialId = id;
                    }

                    // Create frame properties
                    var frameProp = new FrameProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                        Name = name,
                        MaterialId = materialId,
                        Shape = "PIPE"
                    };

                    // Set dimensions
                    frameProp.Dimensions["outerDiameter"] = od;
                    frameProp.Dimensions["wallThickness"] = thickness;

                    frameProperties[name] = frameProp;
                }
            }

            // Process rectangular properties
            var rectMatches = rectPattern.Matches(frameSectionsSection);
            foreach (Match match in rectMatches)
            {
                if (match.Groups.Count >= 6 && !iSectionPattern.IsMatch(match.Value) && !hssPattern.IsMatch(match.Value))
                {
                    string name = match.Groups[1].Value;
                    string materialName = match.Groups[2].Value;
                    string shape = match.Groups[3].Value;
                    double depth = Convert.ToDouble(match.Groups[4].Value);
                    double width = Convert.ToDouble(match.Groups[5].Value);

                    // Look up material ID
                    string materialId = null;
                    if (_materialIdsByName.TryGetValue(materialName, out string id))
                    {
                        materialId = id;
                    }

                    // Create frame properties
                    var frameProp = new FrameProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                        Name = name,
                        MaterialId = materialId,
                        Shape = "RECT"
                    };

                    // Set dimensions
                    frameProp.Dimensions["depth"] = depth;
                    frameProp.Dimensions["width"] = width;

                    frameProperties[name] = frameProp;
                }
            }

            return new List<FrameProperties>(frameProperties.Values);
        }

        // Extracts the shape type from the shape name
       
        private string ExtractShapeType(string shapeName)
        {
            if (string.IsNullOrEmpty(shapeName))
                return "";

            // For standard shapes like W12X26, extract the first letter or letters
            var match = Regex.Match(shapeName, @"^([A-Za-z]+)");
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpper();
            }

            // For named shapes like "Steel I/Wide Flange"
            if (shapeName.Contains("I/Wide Flange"))
                return "W";
            if (shapeName.Contains("HSS") || shapeName.Contains("Tube"))
                return "HSS";
            if (shapeName.Contains("Pipe"))
                return "PIPE";
            if (shapeName.Contains("Channel"))
                return "C";
            if (shapeName.Contains("Angle"))
                return "L";
            if (shapeName.Contains("Rectangular"))
                return "RECT";
            if (shapeName.Contains("Circle"))
                return "CIRCLE";

            return shapeName;
        }
    }
}
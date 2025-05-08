using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Properties;
using Core.Models.Properties.Materials;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Properties
{
    public class FramePropertiesExport
    {
        private IModel _model;
        private string _lengthUnit;
        private RAMExporter _exporter;
        private Dictionary<string, string> _framePropMappings = new Dictionary<string, string>();

        public FramePropertiesExport(IModel model, RAMExporter exporter, string lengthUnit = "inches")
        {
            _model = model;
            _exporter = exporter;
            _lengthUnit = lengthUnit;
        }

        public List<FrameProperties> Export()
        {
            var frameProperties = new List<FrameProperties>();
            var processedSections = new HashSet<string>(); // Avoid duplicate sections

            try
            {
                // Process each story to extract beam and column sections
                IStories ramStories = _model.GetStories();
                if (ramStories != null && ramStories.GetCount() > 0)
                {
                    for (int i = 0; i < ramStories.GetCount(); i++)
                    {
                        IStory ramStory = ramStories.GetAt(i);
                        if (ramStory == null)
                            continue;

                        // Extract frame properties from beams
                        ExtractFramePropertiesFromBeams(ramStory, processedSections, frameProperties);

                        // Extract frame properties from columns
                        ExtractFramePropertiesFromColumns(ramStory, processedSections, frameProperties);
                    }
                }

                // Process braces
                ExtractFramePropertiesFromBraces(processedSections, frameProperties);

                // Update mappings in ModelMappingUtility
                ModelMappingUtility.SetFramePropertyMappings(_framePropMappings);

                // If no sections were found, add a default one
                if (frameProperties.Count == 0)
                {
                    var defaultProp = CreateDefaultFrameProperties();
                    frameProperties.Add(defaultProp);
                    _framePropMappings["W10X12"] = defaultProp.Id;
                    ModelMappingUtility.SetFramePropertyMappings(_framePropMappings);
                }

                return frameProperties;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting frame properties: {ex.Message}");

                // Return at least a default section in case of error
                if (frameProperties.Count == 0)
                {
                    var defaultProp = CreateDefaultFrameProperties();
                    frameProperties.Add(defaultProp);
                    _framePropMappings["W10X12"] = defaultProp.Id;
                    ModelMappingUtility.SetFramePropertyMappings(_framePropMappings);
                }

                return frameProperties;
            }
        }

        private void ExtractFramePropertiesFromBeams(IStory story, HashSet<string> processedSections,
                                                   List<FrameProperties> frameProperties)
        {
            // Get beams for this story
            IBeams storyBeams = story.GetBeams();
            if (storyBeams == null || storyBeams.GetCount() == 0)
                return;

            // Process each beam in the story
            for (int j = 0; j < storyBeams.GetCount(); j++)
            {
                IBeam beam = storyBeams.GetAt(j);
                if (beam == null || string.IsNullOrEmpty(beam.strSectionLabel))
                    continue;

                string sectionLabel = beam.strSectionLabel;

                // Skip if we've already processed this section
                if (processedSections.Contains(sectionLabel))
                    continue;

                processedSections.Add(sectionLabel);

                // Get material ID
                string materialId = _exporter.GetOrCreateMaterialId(beam.lMaterialID, beam.eMaterial, _model);

                // Extract shape from section label
                string shape = ExtractShapeFromSectionLabel(sectionLabel);

                // Create a frame property for this section
                var frameProp = new FrameProperties
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                    Name = sectionLabel,
                    MaterialId = materialId,
                    Shape = shape
                };

                // Extract dimensions from the section label
                ParseSectionDimensions(frameProp);

                frameProperties.Add(frameProp);
                _framePropMappings[sectionLabel] = frameProp.Id;
            }
        }

        private void ExtractFramePropertiesFromColumns(IStory story, HashSet<string> processedSections,
                                                    List<FrameProperties> frameProperties)
        {
            // Get columns for this story
            IColumns storyColumns = story.GetColumns();
            if (storyColumns == null || storyColumns.GetCount() == 0)
                return;

            // Process each column in the story
            for (int j = 0; j < storyColumns.GetCount(); j++)
            {
                IColumn column = storyColumns.GetAt(j);
                if (column == null || string.IsNullOrEmpty(column.strSectionLabel))
                    continue;

                string sectionLabel = column.strSectionLabel;

                // Skip if we've already processed this section
                if (processedSections.Contains(sectionLabel))
                    continue;

                processedSections.Add(sectionLabel);

                // Get material ID
                string materialId = _exporter.GetOrCreateMaterialId(column.lMaterialID, column.eMaterial, _model);

                // Extract shape from section label
                string shape = ExtractShapeFromSectionLabel(sectionLabel);

                // Create a frame property for this section
                var frameProp = new FrameProperties
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                    Name = sectionLabel,
                    MaterialId = materialId,
                    Shape = shape
                };

                // Extract dimensions from the section label
                ParseSectionDimensions(frameProp);

                frameProperties.Add(frameProp);
                _framePropMappings[sectionLabel] = frameProp.Id;
            }
        }

        private void ExtractFramePropertiesFromBraces(HashSet<string> processedSections,
                                                    List<FrameProperties> frameProperties)
        {
            // Get vertical braces
            IVerticalBraces verticalBraces = _model.GetVerticalBraces();
            if (verticalBraces != null)
            {
                for (int i = 0; i < verticalBraces.GetCount(); i++)
                {
                    IVerticalBrace brace = verticalBraces.GetAt(i);
                    if (brace == null || string.IsNullOrEmpty(brace.strSectionLabel))
                        continue;

                    string sectionLabel = brace.strSectionLabel;

                    // Skip if we've already processed this section
                    if (processedSections.Contains(sectionLabel))
                        continue;

                    processedSections.Add(sectionLabel);

                    // Get material ID
                    string materialId = _exporter.GetOrCreateMaterialId(brace.lMaterialID, brace.eMaterial, _model);

                    // Extract shape from section label
                    string shape = ExtractShapeFromSectionLabel(sectionLabel);

                    // Create a frame property for this section
                    var frameProp = new FrameProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                        Name = sectionLabel,
                        MaterialId = materialId,
                        Shape = shape
                    };

                    // Extract dimensions from the section label
                    ParseSectionDimensions(frameProp);

                    frameProperties.Add(frameProp);
                    _framePropMappings[sectionLabel] = frameProp.Id;
                }
            }
        }

        private void ParseSectionDimensions(FrameProperties frameProp)
        {
            try
            {
                // Parse dimensions from section names like W14X90, HSS6X6X3/8, etc.
                string name = frameProp.Name;

                if (frameProp.Shape == "W" && name.Contains("X"))
                {
                    // W-shapes: W14X90 means 14" deep, ~90 lbs/ft
                    string[] parts = name.Substring(1).Split('X');
                    if (parts.Length >= 2 && double.TryParse(parts[0], out double depth))
                    {
                        frameProp.Dimensions["depth"] = depth;
                        frameProp.Dimensions["width"] = depth * 0.7; // Approximate width based on depth
                        frameProp.Dimensions["webThickness"] = depth * 0.03; // Approximate web thickness
                        frameProp.Dimensions["flangeThickness"] = depth * 0.05; // Approximate flange thickness
                    }
                }
                else if (frameProp.Shape == "HSS" && name.Contains("X"))
                {
                    // HSS shapes: HSS6X6X3/8 means 6"x6" with 3/8" thickness
                    string[] parts = name.Substring(3).Split('X');
                    if (parts.Length >= 2)
                    {
                        if (double.TryParse(parts[0], out double depth))
                            frameProp.Dimensions["depth"] = depth;

                        if (double.TryParse(parts[1], out double width))
                            frameProp.Dimensions["width"] = width;

                        // If thickness is provided (HSS6X6X3/8)
                        if (parts.Length >= 3)
                        {
                            string thicknessStr = parts[2];
                            // Handle fractions like 3/8
                            if (thicknessStr.Contains("/"))
                            {
                                string[] fractionParts = thicknessStr.Split('/');
                                if (fractionParts.Length == 2 &&
                                    double.TryParse(fractionParts[0], out double numerator) &&
                                    double.TryParse(fractionParts[1], out double denominator))
                                {
                                    frameProp.Dimensions["wallThickness"] = numerator / denominator;
                                }
                            }
                            else if (double.TryParse(thicknessStr, out double thickness))
                            {
                                frameProp.Dimensions["wallThickness"] = thickness;
                            }
                        }
                        else
                        {
                            // Default thickness if not specified
                            frameProp.Dimensions["wallThickness"] = 0.25;
                        }
                    }
                }
                // Add parsing for other shapes as needed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing section dimensions: {ex.Message}");
                // Initialize default dimensions
                frameProp.InitializeDefaultDimensions();
            }
        }

        private string ExtractShapeFromSectionLabel(string sectionLabel)
        {
            // Extract shape (first letters before numbers)
            string shape = "";
            int i = 0;
            while (i < sectionLabel.Length && !char.IsDigit(sectionLabel[i]))
            {
                shape += sectionLabel[i];
                i++;
            }

            return shape.Trim();
        }

        private FrameProperties CreateDefaultFrameProperties()
        {
            // Get the default steel material ID
            string steelMaterialId = _exporter.GetDefaultSteelMaterialId();

            var defaultProp = new FrameProperties
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                Name = "W10X12",
                MaterialId = steelMaterialId,
                Shape = "W"
            };

            // Set standard dimensions for W10X12
            defaultProp.Dimensions["depth"] = 10.0;      // inches
            defaultProp.Dimensions["width"] = 4.0;       // inches
            defaultProp.Dimensions["webThickness"] = 0.19;    // inches
            defaultProp.Dimensions["flangeThickness"] = 0.3;  // inches

            return defaultProp;
        }
    }
}
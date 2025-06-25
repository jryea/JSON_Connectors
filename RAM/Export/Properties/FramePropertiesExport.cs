using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Properties
{
    public class FramePropertiesExport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;
        private readonly Dictionary<string, string> _framePropMappings = new Dictionary<string, string>();

        public FramePropertiesExport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
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

                // Get material ID based on RAM material type
                string materialId = _materialProvider.GetMaterialIdByType(beam.eMaterial);

                // Create a frame property with the appropriate type
                var frameProp = new FrameProperties
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                    Name = sectionLabel,
                    MaterialId = materialId,
                    Type = beam.eMaterial == EMATERIALTYPES.EConcreteMat ?
                        FrameMaterialType.Concrete :
                        FrameMaterialType.Steel
                };

                // Initialize appropriate property class based on material type
                if (frameProp.Type == FrameMaterialType.Steel)
                {
                    // Extract shape from section label
                    string shape = ExtractShapeFromSectionLabel(sectionLabel);

                    frameProp.SteelProps = new SteelFrameProperties
                    {
                        SectionName = sectionLabel
                    };

                    // Try to parse the section type with enhanced joist girder support
                    if (Enum.TryParse(shape, out SteelSectionType sectionType))
                    {
                        frameProp.SteelProps.SectionType = sectionType;
                    }
                    else
                    {
                        // Enhanced fallback logic for unrecognized shapes
                        if (sectionLabel.ToUpper().Contains("JOIST"))
                        {
                            // Check if it's a joist girder or bar joist
                            if (IsJoistGirderPattern(sectionLabel))
                            {
                                frameProp.SteelProps.SectionType = SteelSectionType.JOIST_GIRDER;
                            }
                            else if (IsBarJoistPattern(sectionLabel))
                            {
                                frameProp.SteelProps.SectionType = SteelSectionType.BAR_JOIST;
                            }
                            else
                            {
                                frameProp.SteelProps.SectionType = SteelSectionType.W; // Default
                            }
                        }
                        else
                        {
                            // Default to W section for unrecognized steel shapes
                            frameProp.SteelProps.SectionType = SteelSectionType.W;
                        }
                    }
                }
                else
                {
                    frameProp.ConcreteProps = new ConcreteFrameProperties
                    {
                        SectionType = ConcreteSectionType.Rectangular,
                        SectionName = sectionLabel
                    };

                    // Add default dimensions
                    frameProp.ConcreteProps.Dimensions["width"] = "24";
                    frameProp.ConcreteProps.Dimensions["depth"] = "24";
                }

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

                // Get material ID based on RAM material type
                string materialId = _materialProvider.GetMaterialIdByType(column.eMaterial);

                // Create a frame property with the appropriate type
                var frameProp = new FrameProperties
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                    Name = sectionLabel,
                    MaterialId = materialId,
                    Type = column.eMaterial == EMATERIALTYPES.EConcreteMat ?
                        FrameMaterialType.Concrete :
                        FrameMaterialType.Steel
                };

                // Initialize appropriate property class based on material type
                if (frameProp.Type == FrameMaterialType.Steel)
                {
                    // Extract shape from section label
                    string shape = ExtractShapeFromSectionLabel(sectionLabel);

                    frameProp.SteelProps = new SteelFrameProperties
                    {
                        SectionName = sectionLabel
                    };

                    // Try to parse the section type
                    if (Enum.TryParse(shape, out SteelSectionType sectionType))
                    {
                        frameProp.SteelProps.SectionType = sectionType;
                    }
                    else
                    {
                        // Default to W section
                        frameProp.SteelProps.SectionType = SteelSectionType.W;
                    }
                }
                else
                {
                    frameProp.ConcreteProps = new ConcreteFrameProperties
                    {
                        SectionType = ConcreteSectionType.Rectangular,
                        SectionName = sectionLabel
                    };

                    // Add default dimensions
                    frameProp.ConcreteProps.Dimensions["width"] = "24";
                    frameProp.ConcreteProps.Dimensions["depth"] = "24";
                }

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

                    // Get material ID based on RAM material type
                    string materialId = _materialProvider.GetMaterialIdByType(brace.eMaterial);

                    // Create a frame property with the appropriate type
                    var frameProp = new FrameProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                        Name = sectionLabel,
                        MaterialId = materialId,
                        Type = brace.eMaterial == EMATERIALTYPES.EConcreteMat ?
                            FrameMaterialType.Concrete :
                            FrameMaterialType.Steel
                    };

                    // Initialize appropriate property class based on material type
                    if (frameProp.Type == FrameMaterialType.Steel)
                    {
                        // Extract shape from section label
                        string shape = ExtractShapeFromSectionLabel(sectionLabel);

                        frameProp.SteelProps = new SteelFrameProperties
                        {
                            SectionName = sectionLabel
                        };

                        // Try to parse the section type
                        if (Enum.TryParse(shape, out SteelSectionType sectionType))
                        {
                            frameProp.SteelProps.SectionType = sectionType;
                        }
                        else
                        {
                            // Default to HSS section for braces
                            frameProp.SteelProps.SectionType = SteelSectionType.HSS;
                        }
                    }
                    else
                    {
                        frameProp.ConcreteProps = new ConcreteFrameProperties
                        {
                            SectionType = ConcreteSectionType.Rectangular,
                            SectionName = sectionLabel
                        };

                        // Add default dimensions
                        frameProp.ConcreteProps.Dimensions["width"] = "12";
                        frameProp.ConcreteProps.Dimensions["depth"] = "12";
                    }

                    frameProperties.Add(frameProp);
                    _framePropMappings[sectionLabel] = frameProp.Id;
                }
            }
        }
        private string ExtractShapeFromSectionLabel(string sectionLabel)
        {
            // Handle joist girders first (pattern: {number}G...)
            if (System.Text.RegularExpressions.Regex.IsMatch(sectionLabel, @"^\d+G", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return "JOIST_GIRDER";
            }

            // Handle bar joists (pattern: {number}K... or {number}LH...)  
            if (System.Text.RegularExpressions.Regex.IsMatch(sectionLabel, @"^\d+(K|LH)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return "BAR_JOIST";
            }

            // Extract shape (first letters before numbers) for traditional sections
            string shape = "";
            int i = 0;
            while (i < sectionLabel.Length && !char.IsDigit(sectionLabel[i]))
            {
                shape += sectionLabel[i];
                i++;
            }

            return shape.Trim();
        }

        private bool IsJoistGirderPattern(string sectionLabel)
        {
            // Match pattern like "40G5N15K" - starts with number followed by G
            return System.Text.RegularExpressions.Regex.IsMatch(sectionLabel, @"^\d+G",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private bool IsBarJoistPattern(string sectionLabel)
        {
            // Match patterns like "22K4", "18LH03" - starts with number followed by K or LH
            return System.Text.RegularExpressions.Regex.IsMatch(sectionLabel, @"^\d+(K|LH)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private FrameProperties CreateDefaultFrameProperties()
        {
            // Get the steel material ID
            string steelMaterialId = _materialProvider.GetSteelMaterialId();

            var defaultProp = new FrameProperties
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                Name = "W10X12",
                MaterialId = steelMaterialId,
                Type = FrameMaterialType.Steel
            };

            // Initialize steel properties
            defaultProp.SteelProps = new SteelFrameProperties
            {
                SectionName = "W10X12",
                SectionType = SteelSectionType.W
            };

            return defaultProp;
        }
    }
}
// FramePropertiesExport.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Properties;
using Core.Models.Properties.Floors;
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
        // This is the key addition - maintain the mappings as a class field
        private Dictionary<string, string> _framePropMappings;

        public FramePropertiesExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
            _framePropMappings = new Dictionary<string, string>();
        }

        public (List<FrameProperties> Properties, Dictionary<string, string> Mapping) Export(List<Material> materials)
        {
            var frameProperties = new List<FrameProperties>();
            var processedSections = new HashSet<string>(); // Avoid duplicate sections

            try
            {
                // Find steel material ID
                string steelMaterialId = materials.FirstOrDefault(m => m.Type.ToLower() == "steel")?.Id;
                if (string.IsNullOrEmpty(steelMaterialId))
                {
                    // If no steel material found, add a default one
                    var steelMaterial = new Material
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                        Name = "Default Steel",
                        Type = "Steel"
                    };
                    materials.Add(steelMaterial);
                    steelMaterialId = steelMaterial.Id;
                }

                // Find concrete material ID
                string concreteMaterialId = materials.FirstOrDefault(m => m.Type.ToLower() == "concrete")?.Id;
                if (string.IsNullOrEmpty(concreteMaterialId))
                {
                    // If no concrete material found, add a default one
                    var concreteMaterial = new Material
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                        Name = "Concrete",
                        Type = "Concrete"
                    };
                    materials.Add(concreteMaterial);
                    concreteMaterialId = concreteMaterial.Id;
                }

                // Get all stories from RAM
                IStories ramStories = _model.GetStories();
                if (ramStories == null || ramStories.GetCount() == 0)
                    return (frameProperties, _framePropMappings);

                // Process each story
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory ramStory = ramStories.GetAt(i);
                    if (ramStory == null)
                        continue;

                    // Extract beam sections
                    IBeams storyBeams = ramStory.GetBeams();
                    if (storyBeams != null && storyBeams.GetCount() > 0)
                    {
                        ExtractSectionsFromBeams(storyBeams, processedSections, frameProperties, steelMaterialId);
                    }

                    // Extract column sections
                    IColumns storyColumns = ramStory.GetColumns();
                    if (storyColumns != null && storyColumns.GetCount() > 0)
                    {
                        ExtractSectionsFromColumns(storyColumns, processedSections, frameProperties, steelMaterialId);
                    }

                    // Add code for braces if needed
                }

                // If no sections were found, add a default one
                if (frameProperties.Count == 0)
                {
                    var defaultProp = CreateDefaultFrameProperties(steelMaterialId);
                    frameProperties.Add(defaultProp);
                    _framePropMappings["W10X12"] = defaultProp.Id;
                }

                return (frameProperties, _framePropMappings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting frame properties: {ex.Message}");

                // Return at least a default section in case of error
                if (frameProperties.Count == 0)
                {
                    string defaultMaterialId = materials.FirstOrDefault()?.Id ??
                        IdGenerator.Generate(IdGenerator.Properties.MATERIAL);

                    var defaultProp = CreateDefaultFrameProperties(defaultMaterialId);
                    frameProperties.Add(defaultProp);
                    _framePropMappings["W10X12"] = defaultProp.Id;
                }

                return (frameProperties, _framePropMappings);
            }
        }

        private void ExtractSectionsFromBeams(IBeams beams, HashSet<string> processedSections,
                                             List<FrameProperties> frameProperties,
                                             string materialId)
        {
            for (int j = 0; j < beams.GetCount(); j++)
            {
                IBeam beam = beams.GetAt(j);
                if (beam == null || string.IsNullOrEmpty(beam.strSectionLabel))
                    continue;

                string sectionLabel = beam.strSectionLabel;

                // Skip if we've already processed this section
                if (processedSections.Contains(sectionLabel))
                    continue;

                processedSections.Add(sectionLabel);

                // Extract shape from section label (e.g., "W" from "W14X90")
                string shape = ExtractShapeFromSectionLabel(sectionLabel);

                // Create a frame properties object for this section
                var frameProp = new FrameProperties
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                    Name = sectionLabel,
                    MaterialId = materialId,
                    Shape = shape
                };

                frameProperties.Add(frameProp);
                _framePropMappings[sectionLabel] = frameProp.Id;
            }
        }

        private void ExtractSectionsFromColumns(IColumns columns, HashSet<string> processedSections,
                                               List<FrameProperties> frameProperties,
                                               string materialId)
        {
            for (int j = 0; j < columns.GetCount(); j++)
            {
                IColumn column = columns.GetAt(j);
                if (column == null || string.IsNullOrEmpty(column.strSectionLabel))
                    continue;

                string sectionLabel = column.strSectionLabel;

                // Skip if we've already processed this section
                if (processedSections.Contains(sectionLabel))
                    continue;

                processedSections.Add(sectionLabel);

                // Extract shape from section label (e.g., "W" from "W14X90")
                string shape = ExtractShapeFromSectionLabel(sectionLabel);

                // Create a frame properties object for this section
                var frameProp = new FrameProperties
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                    Name = sectionLabel,
                    MaterialId = materialId,
                    Shape = shape
                };

                frameProperties.Add(frameProp);
                _framePropMappings[sectionLabel] = frameProp.Id;
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

        private FrameProperties CreateDefaultFrameProperties(string materialId)
        {
            return new FrameProperties
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.FRAME_PROPERTIES),
                Name = "W10X12",
                MaterialId = materialId,
                Shape = "W"
            };
        }
    }
}
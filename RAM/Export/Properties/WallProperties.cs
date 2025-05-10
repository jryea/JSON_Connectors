using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Properties
{
    public class WallPropertiesExport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public WallPropertiesExport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        public List<WallProperties> Export()
        {
            var wallProperties = new List<WallProperties>();
            var processedThicknesses = new HashSet<double>(); // Track unique thicknesses

            try
            {
                // Get concrete material ID
                string concreteMaterialId = _materialProvider.GetConcreteMaterialId();

                // Get all stories from RAM
                IStories ramStories = _model.GetStories();
                if (ramStories == null || ramStories.GetCount() == 0)
                    return wallProperties;

                // Loop through all stories to find unique wall thicknesses
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory ramStory = ramStories.GetAt(i);
                    if (ramStory == null)
                        continue;

                    IWalls storyWalls = ramStory.GetWalls();
                    if (storyWalls == null || storyWalls.GetCount() == 0)
                        continue;

                    // Process each wall in the story
                    for (int j = 0; j < storyWalls.GetCount(); j++)
                    {
                        IWall ramWall = storyWalls.GetAt(j);
                        if (ramWall == null)
                            continue;

                        // Get the thickness from RAM and convert to model units
                        double thickness = UnitConversionUtils.ConvertFromInches(ramWall.dThickness, _lengthUnit);

                        // Round to avoid floating point issues
                        thickness = Math.Round(thickness, 2);

                        // Skip if we've already processed this thickness
                        if (processedThicknesses.Contains(thickness))
                            continue;

                        // Add to processed set
                        processedThicknesses.Add(thickness);

                        // Create a wall property for this thickness
                        WallProperties wallProperty = new WallProperties
                        {
                            Id = IdGenerator.Generate(IdGenerator.Properties.WALL_PROPERTIES),
                            Name = $"Wall_{thickness:F1}\"",
                            MaterialId = concreteMaterialId,
                            Thickness = thickness
                        };

                        wallProperties.Add(wallProperty);
                        Console.WriteLine($"Created wall property with thickness {thickness:F2} {_lengthUnit}");
                    }
                }

                // If no wall properties were found in the model, create a default one
                if (wallProperties.Count == 0)
                {
                    WallProperties defaultWallProperty = new WallProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.WALL_PROPERTIES),
                        Name = "Default Wall",
                        MaterialId = concreteMaterialId,
                        Thickness = 8.0 // Default thickness in model units
                    };

                    wallProperties.Add(defaultWallProperty);
                    Console.WriteLine("Created default wall property");
                }

                // Create a mapping from thickness to property ID for later use
                Dictionary<double, string> thicknessToIdMapping = new Dictionary<double, string>();
                foreach (var wallProp in wallProperties)
                {
                    thicknessToIdMapping[wallProp.Thickness] = wallProp.Id;
                }

                // Add this mapping to the ModelMappingUtility
                ModelMappingUtility.SetWallThicknessMapping(thicknessToIdMapping);

                return wallProperties;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting wall properties from RAM: {ex.Message}");
                return wallProperties;
            }
        }
    }
}
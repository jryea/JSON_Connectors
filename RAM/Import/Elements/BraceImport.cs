using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    public class BraceImport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;

        public BraceImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Brace> braces, IEnumerable<Level> levels,
                         IEnumerable<FrameProperties> frameProperties,
                         IEnumerable<Material> materials)
        {
            if (braces == null || !braces.Any() || levels == null || !levels.Any())
            {
                Console.WriteLine("No braces or levels provided for import.");
                return 0;
            }

            // Get stories from RAM model
            IStories ramStories = _model.GetStories();
            if (ramStories == null || ramStories.GetCount() == 0)
            {
                Console.WriteLine("No stories found in RAM model.");
                return 0;
            }

            // Create a mapping of Level.Id to RAM Story.lUID
            var levelToStoryMapping = ImportHelpers.CreateLevelToStoryMapping(levels, ramStories);

            // Log the mapping for debugging
            Console.WriteLine("Level to Story Mapping:");
            foreach (var kvp in levelToStoryMapping)
            {
                Console.WriteLine($"Level ID: {kvp.Key}, Story ID: {kvp.Value}");
            }

            // Create a lookup for RAM stories by Level.Id
            var ramStoryByLevelId = new Dictionary<string, IStory>();
            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory ramStory = ramStories.GetAt(i);
                string storyId = ramStory.lUID.ToString();

                if (levelToStoryMapping.ContainsValue(storyId))
                {
                    string levelId = levelToStoryMapping.First(kvp => kvp.Value == storyId).Key;
                    ramStoryByLevelId[levelId] = ramStory;
                    Console.WriteLine($"Mapped Core Level {levelId} to RAM Story {ramStory.strLabel}");
                }
            }

            // Get vertical braces interface from the model
            IVerticalBraces verticalBraces = _model.GetVerticalBraces();
            if (verticalBraces == null)
            {
                Console.WriteLine("Could not get vertical braces interface from RAM model.");
                return 0;
            }

            // Import braces
            int count = 0;
            var processedBraces = new HashSet<string>();
            foreach (var brace in braces)
            {
                if (!IsValidBrace(brace))
                {
                    Console.WriteLine("Skipping brace with incomplete data.");
                    continue;
                }

                // Map the brace's TopLevelId and BaseLevelId to the corresponding IStory.lUID
                if (!levelToStoryMapping.TryGetValue(brace.TopLevelId, out string topStoryId) ||
                    !TryGetStoryByLUID(ramStories, topStoryId, out IStory topStory))
                {
                    Console.WriteLine($"No RAM story found for top level {brace.TopLevelId}. Skipping brace.");
                    continue;
                }

                if (!levelToStoryMapping.TryGetValue(brace.BaseLevelId, out string baseStoryId) ||
                    !TryGetStoryByLUID(ramStories, baseStoryId, out IStory baseStory))
                {
                    Console.WriteLine($"No RAM story found for base level {brace.BaseLevelId}. Skipping brace.");
                    continue;
                }

                // Convert coordinates to inches
                double topX = UnitConversionUtils.ConvertToInches(brace.EndPoint.X, _lengthUnit);
                double topY = UnitConversionUtils.ConvertToInches(brace.EndPoint.Y, _lengthUnit);
                double baseX = UnitConversionUtils.ConvertToInches(brace.StartPoint.X, _lengthUnit);
                double baseY = UnitConversionUtils.ConvertToInches(brace.StartPoint.Y, _lengthUnit);

                // Create a unique key for this brace
                string braceKey = $"{topX:F2}_{topY:F2}_{baseX:F2}_{baseY:F2}_{brace.TopLevelId}_{brace.BaseLevelId}";

                if (processedBraces.Contains(braceKey))
                {
                    Console.WriteLine("Skipping duplicate brace.");
                    continue;
                }

                processedBraces.Add(braceKey);

                // Get material type
                EMATERIALTYPES braceMaterial = ImportHelpers.GetRAMMaterialType(
                    brace.FramePropertiesId,
                    frameProperties,
                    materials);

                try
                {
                    // Add the brace to the RAM model
                    IVerticalBrace ramBrace = verticalBraces.Add(
                        braceMaterial,
                        topStory.lUID,  // Top story ID
                        topX,           // Top X coordinate
                        topY,           // Top Y coordinate
                        0,              // Top offset
                        baseStory.lUID, // Base story ID
                        baseX,          // Base X coordinate
                        baseY,          // Base Y coordinate
                        0               // Base offset
                    );

                    if (ramBrace != null)
                    {
                        count++;
                        Console.WriteLine($"Added brace from story {baseStory.strLabel} to {topStory.strLabel}");
                    }
                    else
                    {
                        Console.WriteLine("Failed to create brace in RAM.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating brace: {ex.Message}");
                }
            }


            return count;
        }

        private bool TryGetStoryByLUID(IStories ramStories, string storyId, out IStory story)
        {
            story = null;
            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory currentStory = ramStories.GetAt(i);
                if (currentStory.lUID.ToString() == storyId)
                {
                    story = currentStory;
                    return true;
                }
            }
            return false;
        }

        private bool IsValidBrace(Brace brace)
        {
            return brace != null &&
                   brace.StartPoint != null &&
                   brace.EndPoint != null &&
                   !string.IsNullOrEmpty(brace.BaseLevelId) &&
                   !string.IsNullOrEmpty(brace.TopLevelId);
        }
    }
}

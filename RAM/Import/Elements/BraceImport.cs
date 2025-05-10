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
        private readonly MaterialProvider _materialProvider;

        public BraceImport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Brace> braces, IEnumerable<Level> levels,
                         IEnumerable<FrameProperties> frameProperties)
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

                // Map the brace's TopLevelId and BaseLevelId to the corresponding story UIDs
                string topStoryUid = ModelMappingUtility.GetStoryUidForLevelId(brace.TopLevelId);
                string baseStoryUid = ModelMappingUtility.GetStoryUidForLevelId(brace.BaseLevelId);

                if (string.IsNullOrEmpty(topStoryUid))
                {
                    Console.WriteLine($"No RAM story found for top level {brace.TopLevelId}. Skipping brace.");
                    continue;
                }

                if (string.IsNullOrEmpty(baseStoryUid))
                {
                    Console.WriteLine($"No RAM story found for base level {brace.BaseLevelId}. Skipping brace.");
                    continue;
                }

                // Get the actual RAM stories by UID
                IStory topStory = RAMHelpers.GetStoryByUid(_model, topStoryUid);
                IStory baseStory = RAMHelpers.GetStoryByUid(_model, baseStoryUid);

                if (topStory == null)
                {
                    Console.WriteLine($"Could not find top story with UID {topStoryUid}. Skipping brace.");
                    continue;
                }

                if (baseStory == null)
                {
                    Console.WriteLine($"Could not find base story with UID {baseStoryUid}. Skipping brace.");
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

                // Get material type using the MaterialProvider
                EMATERIALTYPES braceMaterial = _materialProvider.GetRAMMaterialType(
                    brace.FramePropertiesId,
                    frameProperties);

                try
                {
                    // Get vertical braces interface from the model
                    IVerticalBraces verticalBraces = _model.GetVerticalBraces();
                    if (verticalBraces == null)
                    {
                        Console.WriteLine("Could not get vertical braces interface from RAM model.");
                        continue;
                    }

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
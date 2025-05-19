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
                         IEnumerable<FrameProperties> frameProperties,
                         Dictionary<string, string> levelToFloorTypeMapping)
        {
            try
            {
                if (braces == null || !braces.Any() || levels == null || !levels.Any())
                {
                    Console.WriteLine("No braces or levels provided for import.");
                    return 0;
                }

                // Get RAM stories
                IStories ramStories = _model.GetStories();
                if (ramStories == null || ramStories.GetCount() == 0)
                {
                    Console.WriteLine("No stories found in RAM model.");
                    return 0;
                }

                Console.WriteLine("Beginning brace import...");

                // Create mappings from Core level IDs to RAM story UIDs
                Dictionary<string, int> levelIdToStoryUid = new Dictionary<string, int>();

                // Map levels to stories
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory story = ramStories.GetAt(i);
                    if (story == null) continue;

                    // Find the matching level
                    Level matchingLevel = FindMatchingLevel(story, levels);
                    if (matchingLevel != null)
                    {
                        levelIdToStoryUid[matchingLevel.Id] = story.lUID;
                        Console.WriteLine($"Mapped level {matchingLevel.Name} (ID: {matchingLevel.Id}) to story {story.strLabel} (UID: {story.lUID})");
                    }
                }

                // Get the vertical braces interface
                IVerticalBraces verticalBraces = _model.GetVerticalBraces();
                if (verticalBraces == null)
                {
                    Console.WriteLine("Could not get vertical braces interface from RAM model.");
                    return 0;
                }

                // Track processed braces to avoid duplicates
                var processedBraces = new HashSet<string>();

                // Import braces
                int count = 0;
                foreach (var brace in braces)
                {
                    if (brace.StartPoint == null || brace.EndPoint == null ||
                        string.IsNullOrEmpty(brace.TopLevelId) || string.IsNullOrEmpty(brace.BaseLevelId))
                    {
                        Console.WriteLine("Skipping brace with incomplete data.");
                        continue;
                    }

                    // Get the RAM story UID for the top level
                    if (!levelIdToStoryUid.TryGetValue(brace.TopLevelId, out int topStoryUid))
                    {
                        Console.WriteLine($"No story mapping found for top level {brace.TopLevelId}");
                        continue;
                    }

                    // Get the RAM story UID for the base level
                    int baseStoryUid;

                    // If we can't find a mapping for the base level, use -1 (foundation level in RAM)
                    if (!levelIdToStoryUid.TryGetValue(brace.BaseLevelId, out baseStoryUid))
                    {
                        baseStoryUid = -1;
                    }

                    // Convert coordinates to inches
                    double topX = Math.Round(UnitConversionUtils.ConvertToInches(brace.EndPoint.X, _lengthUnit), 6);
                    double topY = Math.Round(UnitConversionUtils.ConvertToInches(brace.EndPoint.Y, _lengthUnit), 6);
                    double baseX = Math.Round(UnitConversionUtils.ConvertToInches(brace.StartPoint.X, _lengthUnit), 6);
                    double baseY = Math.Round(UnitConversionUtils.ConvertToInches(brace.StartPoint.Y, _lengthUnit), 6);

                    // Create a unique key for this brace
                    string braceKey = $"{topX:F2}_{topY:F2}_{baseX:F2}_{baseY:F2}_{topStoryUid}_{baseStoryUid}";

                    if (processedBraces.Contains(braceKey))
                    {
                        Console.WriteLine("Skipping duplicate brace.");
                        continue;
                    }

                    processedBraces.Add(braceKey);

                    // Get material type using MaterialProvider
                    EMATERIALTYPES braceMaterial = _materialProvider.GetRAMMaterialType(
                        brace.FramePropertiesId,
                        frameProperties);

                    try
                    {
                        Console.WriteLine($"Adding brace: topStoryUID={topStoryUid}, baseStoryUID={baseStoryUid}");

                        // Add the vertical brace to the model
                        IVerticalBrace ramBrace = verticalBraces.Add(
                            braceMaterial,
                            topStoryUid,
                            topX, topY, 0,  // Top point with zero offset
                            baseStoryUid,   // Will be -1 for foundation braces
                            baseX, baseY, 0  // Base point with zero offset
                        );

                        if (ramBrace != null)
                        {
                            count++;
                            string baseStoryDesc = (baseStoryUid == -1) ? "foundation" : $"story {baseStoryUid}";
                            Console.WriteLine($"Added brace from {baseStoryDesc} to story {topStoryUid}");

                            // Set section label if available via frame properties
                            if (!string.IsNullOrEmpty(brace.FramePropertiesId))
                            {
                                var frameProp = frameProperties?.FirstOrDefault(fp => fp.Id == brace.FramePropertiesId);
                                if (frameProp != null && !string.IsNullOrEmpty(frameProp.Name))
                                {
                                    ramBrace.strSectionLabel = frameProp.Name;
                                }
                                else
                                {
                                    ramBrace.strSectionLabel = "HSS4X4X1/4"; // Default if not found
                                }
                            }
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

                Console.WriteLine($"Imported {count} braces");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing braces: {ex.Message}");
                throw;
            }
        }

        private Level FindMatchingLevel(IStory story, IEnumerable<Level> levels)
        {
            if (story == null || levels == null)
                return null;

            // First try to match by name
            string storyName = story.strLabel;
            Level matchingLevel = levels.FirstOrDefault(l =>
                string.Equals(l.Name, storyName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.Name, storyName.Replace("Story ", ""), StringComparison.OrdinalIgnoreCase));

            if (matchingLevel != null)
                return matchingLevel;

            // If no match by name, try to match by elevation
            double storyElevation = story.dElevation;
            matchingLevel = levels.OrderBy(l => Math.Abs(UnitConversionUtils.ConvertToInches(l.Elevation, _lengthUnit) - storyElevation))
                                 .FirstOrDefault();

            return matchingLevel;
        }
    }
}
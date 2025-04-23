// Helpers.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Geometry;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    public static class ImportHelpers
    {
        

        public static EMATERIALTYPES GetRAMMaterialType(string framePropId,
                                                      IEnumerable<FrameProperties> frameProperties,
                                                      IEnumerable<Material> materials,
                                                      bool isJoist = false)
        {
            // If it's a joist, return joist material type regardless of base material
            if (isJoist)
                return EMATERIALTYPES.ESteelJoistMat;

            // Get the frame property to find the material ID
            string materialId = null;
            if (!string.IsNullOrEmpty(framePropId))
            {
                var frameProp = frameProperties?.FirstOrDefault(fp => fp.Id == framePropId);
                if (frameProp != null)
                {
                    materialId = frameProp.MaterialId;
                }
            }

            // Get the actual material
            var material = materials?.FirstOrDefault(m => m.Id == materialId);

            // Determine material type based on the material
            if (material != null && !string.IsNullOrEmpty(material.Type))
            {
                if (material.Type.ToLower().Contains("concrete"))
                    return EMATERIALTYPES.EConcreteMat;
            }

            // Default to steel
            return EMATERIALTYPES.ESteelMat;
        }

        // Get deck properties based on deck type and gage
        public static void GetDeckProperties(string deckType, int deckGage, out double selfWeight)
        {
            if (deckType == "VULCRAFT 1.5VL")
            {
                if (deckGage == 22)
                    selfWeight = 1.6;
                else if (deckGage == 20)
                    selfWeight = 2.0;
                else if (deckGage == 19)
                    selfWeight = 2.3;
                else if (deckGage == 18)
                    selfWeight = 2.6;
                else if (deckGage == 16)
                    selfWeight = 3.3;
                else
                    selfWeight = 2.0; // Default
            }
            else if (deckType == "VULCRAFT 2VL")
            {
                if (deckGage == 22)
                    selfWeight = 1.6;
                else if (deckGage == 20)
                    selfWeight = 1.9;
                else if (deckGage == 19)
                    selfWeight = 2.2;
                else if (deckGage == 18)
                    selfWeight = 2.5;
                else if (deckGage == 16)
                    selfWeight = 3.2;
                else
                    selfWeight = 2.0; // Default
            }
            else if (deckType == "VULCRAFT 3VL")
            {
                if (deckGage == 22)
                    selfWeight = 1.7;
                else if (deckGage == 20)
                    selfWeight = 2.1;
                else if (deckGage == 19)
                    selfWeight = 2.4;
                else if (deckGage == 18)
                    selfWeight = 2.7;
                else if (deckGage == 16)
                    selfWeight = 3.5;
                else
                    selfWeight = 2.0; // Default
            }
            else
            {
                selfWeight = 2.0; // Default value for unknown deck types
            }
        }

        // Helper to get floor types from RAM model by names
        public static List<IFloorType> GetFloorTypes(IModel model, List<string> floorTypeNames)
        {
            List<IFloorType> floorTypes = new List<IFloorType>();
            try
            {
                IFloorTypes ramFloorTypes = model.GetFloorTypes();

                // If no specific floor types are requested, return all floor types
                if (floorTypeNames == null || floorTypeNames.Count == 0)
                {
                    for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                    {
                        floorTypes.Add(ramFloorTypes.GetAt(i));
                    }
                    return floorTypes;
                }

                // Otherwise, find the specific floor types requested
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType floorType = ramFloorTypes.GetAt(i);
                    if (floorTypeNames.Contains(floorType.strLabel))
                    {
                        floorTypes.Add(floorType);
                    }
                }

                // If no matching floor types found, add at least the first available type
                if (floorTypes.Count == 0 && ramFloorTypes.GetCount() > 0)
                {
                    floorTypes.Add(ramFloorTypes.GetAt(0));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting floor types: {ex.Message}");
            }

            return floorTypes;
        }

        // Find level ID for story
        public static string FindLevelIdForStory(IStory story, Dictionary<string, string> levelMappings)
        {
            if (story == null || levelMappings == null || levelMappings.Count == 0)
                return null;

            // Try to find direct mapping by story name
            string storyName = story.strLabel;

            if (levelMappings.TryGetValue(storyName, out string levelId))
                return levelId;

            // Try with "Story" prefix variations
            if (levelMappings.TryGetValue($"Story {storyName}", out levelId) ||
                levelMappings.TryGetValue($"Story{storyName}", out levelId))
                return levelId;

            // Return first level ID as fallback
            return levelMappings.Values.FirstOrDefault();
        }

        // Find level ID for floor type
        public static string FindLevelIdForFloorType(IFloorType floorType, Dictionary<string, string> levelMappings)
        {
            if (floorType == null || levelMappings == null || levelMappings.Count == 0)
                return null;

            // Try to find direct mapping by floor type UID
            string key = $"FloorType_{floorType.lUID}";
            if (levelMappings.TryGetValue(key, out string levelId))
                return levelId;

            // If not found, try by floor type name
            if (levelMappings.TryGetValue(floorType.strLabel, out levelId))
                return levelId;

            // Return first level ID as fallback
            return levelMappings.Values.FirstOrDefault();
        }

        // Find level IDs for all stories associated with a floor type
        public static void FindLevelIdsForFloorTypes(IFloorType floorType, IModel model, Dictionary<string, string> levelMappings)
        {
            if (floorType == null || model == null || levelMappings == null)
                return;

            try
            {
                // Get all stories from the floor type
                IStories ramStories = floorType.GetStories();

                if (ramStories == null || ramStories.GetCount() == 0)
                    return;

                // Iterate through all stories to find those associated with the given floor type
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory story = ramStories.GetAt(i);
                    if (story != null && story.GetFloorType()?.lUID == floorType.lUID)
                    {
                        // Add the story's level ID to the levelMappings dictionary
                        string storyName = story.strLabel;
                        if (!string.IsNullOrEmpty(storyName) && !levelMappings.ContainsKey(storyName))
                        {
                            levelMappings[storyName] = $"FloorType_{floorType.lUID}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding level IDs for floor types: {ex.Message}");
            }
        }

        // Find base level ID for story
        public static string FindBaseLevelIdForStory(IStory story, IModel model, Dictionary<string, string> levelMappings)
        {
            if (story == null || model == null || levelMappings == null || levelMappings.Count == 0)
                return null;

            // Try to find the level below this story
            IStories ramStories = model.GetStories();
            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory belowStory = ramStories.GetAt(i);
                if (belowStory != null && belowStory.dElevation < story.dElevation)
                {
                    return FindLevelIdForStory(belowStory, levelMappings);
                }
            }

            // If no level below found, use the same level ID as top level
            return FindLevelIdForStory(story, levelMappings);
        }

        // Find top level ID for wall
        public static string FindTopLevelIdForWall(IWall wall, IStory currentStory, Dictionary<string, string> levelMappings)
        {
            if (wall == null || currentStory == null || levelMappings == null || levelMappings.Count == 0)
                return null;

            // Try to determine if wall extends to stories above
            // In a real implementation, you'd check wall.lStoriesAbove or similar property
            // For simplicity, we'll just use the current level as top level

            return FindLevelIdForStory(currentStory, levelMappings);
        }

        // Find frame properties ID by section label
        public static string FindFramePropertiesId(string sectionName, Dictionary<string, string> _framePropMappings)
        {
            if (string.IsNullOrEmpty(sectionName))
            {
                Console.WriteLine("BeamExport: Section name is empty, using first available mapping");
                return _framePropMappings.Values.FirstOrDefault();
            }

            // Try to find direct mapping by section name
            if (_framePropMappings.TryGetValue(sectionName, out string framePropsId))
            {
                Console.WriteLine($"BeamExport: Found mapping for section {sectionName} -> {framePropsId}");
                return framePropsId;
            }

            // Try case-insensitive matching
            foreach (var mapping in _framePropMappings)
            {
                if (string.Equals(mapping.Key, sectionName, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"BeamExport: Found case-insensitive mapping for section {sectionName} -> {mapping.Value}");
                    return mapping.Value;
                }
            }

            // If still not found, return first available mapping as fallback
            string fallbackId = _framePropMappings.Values.FirstOrDefault();
            Console.WriteLine($"BeamExport: No mapping found for section {sectionName}, using fallback {fallbackId}");
            return fallbackId;
        }

        // Find wall properties ID by wall
        public static string FindWallPropertiesId(IWall wall, Dictionary<string, string> wallPropMappings)
        {
            if (wall == null || wallPropMappings == null || wallPropMappings.Count == 0)
                return null;

            // Try to find wall property by thickness
            double thickness = wall.dThickness;

            // Look for a wall property with matching thickness
            foreach (var entry in wallPropMappings)
            {
                // This is a simplified approach - in a real implementation,
                // you would need to retrieve the actual wall properties and compare
                if (entry.Key.Contains(thickness.ToString("0.##")))
                    return entry.Value;
            }

            // Return first wall property ID as fallback
            return wallPropMappings.Values.FirstOrDefault();
        }

        // Removes "Story" prefix if present to normalize names
        public static string CleanStoryName(string storyName)
        {
            if (string.IsNullOrEmpty(storyName))
                return storyName;

            if (storyName.StartsWith("Story ", StringComparison.OrdinalIgnoreCase))
            {
                return storyName.Substring(6).Trim();
            }
            else if (storyName.StartsWith("Story", StringComparison.OrdinalIgnoreCase))
            {
                return storyName.Substring(5).Trim();
            }
            return storyName;
        }


        // RAM/Utilities/ImportHelpers.cs
        public static Dictionary<string, string> CreateLevelToFloorTypeMapping(IEnumerable<Level> levels)
        {
            var mapping = new Dictionary<string, string>();

            foreach (var level in levels)
            {
                if (!string.IsNullOrEmpty(level.Id) && !string.IsNullOrEmpty(level.FloorTypeId))
                {
                    mapping[level.Id] = level.FloorTypeId;
                }
            }

            return mapping;
        }

        public static Dictionary<string, string> CreateLevelToStoryMapping(IEnumerable<Level> levels, IStories ramStories)
        {
            var mapping = new Dictionary<string, string>();

            Console.WriteLine("Creating Level to Story Mapping based on Elevation...");
            Console.WriteLine("Levels:");
            foreach (var level in levels)
            {
                Console.WriteLine($"Level ID: {level.Id}, Name: {level.Name}, Elevation: {level.Elevation}");
            }

            Console.WriteLine("RAM Stories:");
            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory story = ramStories.GetAt(i);
                Console.WriteLine($"Story ID: {story.lUID}, Label: {story.strLabel}, Elevation: {story.dElevation}");
            }

            foreach (var level in levels)
            {
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory story = ramStories.GetAt(i);
                    Console.WriteLine($"Comparing Level Elevation '{level.Elevation}' with Story Elevation '{story.dElevation}'...");
                    if (Math.Abs(level.Elevation - story.dElevation) < 0.01) // Allow for small floating-point differences
                    {
                        mapping[level.Id] = story.lUID.ToString();
                        Console.WriteLine($"Mapped Level ID {level.Id} to Story ID {story.lUID}");
                        break;
                    }
                }
            }

            Console.WriteLine("Final Mapping:");
            foreach (var kvp in mapping)
            {
                Console.WriteLine($"Level ID: {kvp.Key}, Story ID: {kvp.Value}");
            }

            return mapping;
        }
    }

    public static class ModelLayoutFilter
    {
        // Filters levels to exclude those with elevation 0.
        public static IEnumerable<Level> GetValidLevels(IEnumerable<Level> levels)
        {
            return levels.Where(level => level.Elevation != 0);
        }

        // Filters floor types to include only those associated with valid levels.
        public static IEnumerable<FloorType> GetValidFloorTypes(IEnumerable<FloorType> floorTypes, IEnumerable<Level> levels)
        {
            var validLevelFloorTypeIds = levels
                .Where(level => level.Elevation != 0)
                .Select(level => level.FloorTypeId)
                .Distinct();

            return floorTypes.Where(floorType => validLevelFloorTypeIds.Contains(floorType.Id));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Properties;
using Core.Models.ModelLayout;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    /// <summary>
    /// Consolidated helper methods for RAM import and export operations
    /// </summary>
    public static class RAMHelpers
    {
        /// <summary>
        /// Get the appropriate RAM material type based on Core model properties
        /// </summary>
        /// <summary>
        /// Get the appropriate RAM material type based on Core model properties
        /// </summary>
        public static EMATERIALTYPES GetRAMMaterialType(string framePropId,
                                                      IEnumerable<FrameProperties> frameProperties,
                                                      IEnumerable<Material> materials,
                                                      bool isJoist = false)
        {
            // Get the frame property to find the material ID and shape
            string materialId = null;
            string shape = null;

            if (!string.IsNullOrEmpty(framePropId))
            {
                var frameProp = frameProperties?.FirstOrDefault(fp => fp.Id == framePropId);
                if (frameProp != null)
                {
                    materialId = frameProp.MaterialId;
                    shape = frameProp.Shape;
                }
            }

            // Only treat as a joist if isJoist is true AND the shape is not W or HSS
            if (isJoist && shape != null)
            {
                // If shape contains 'W' or 'HSS', don't treat as joist
                if (!shape.Contains("W") && !shape.Contains("HSS"))
                {
                    return EMATERIALTYPES.ESteelJoistMat;
                }
            }

            // Get the actual material
            var material = materials?.FirstOrDefault(m => m.Id == materialId);

            // Determine material type based on the material
            if (material != null && !string.IsNullOrEmpty(material.Type))
            {
                string materialType = material.Type.ToLower();

                if (materialType.Contains("concrete"))
                    return EMATERIALTYPES.EConcreteMat;

                if (materialType.Contains("steel"))
                    return EMATERIALTYPES.ESteelMat;
            }

            // Default to steel
            return EMATERIALTYPES.ESteelMat;
        }

        /// <summary>
        /// Get deck properties based on deck type and gage
        /// </summary>
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

        /// <summary>
        /// Helper to get floor types from RAM model by names
        /// </summary>
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

        // Get story by UID from the RAM model
        public static IStory GetStoryByUid(IModel model, string storyUid)
        {
            if (model == null || string.IsNullOrEmpty(storyUid))
                return null;

            IStories ramStories = model.GetStories();
            if (ramStories == null)
                return null;

            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory story = ramStories.GetAt(i);
                if (story != null && story.lUID.ToString() == storyUid)
                {
                    return story;
                }
            }

            return null;
        }

        /// <summary>
        /// Find the lowest story in the RAM model (for foundation level)
        /// </summary>
        public static IStory FindLowestStory(IModel model)
        {
            if (model == null)
                return null;

            IStories ramStories = model.GetStories();
            if (ramStories == null || ramStories.GetCount() == 0)
                return null;

            IStory lowestStory = null;
            double lowestElevation = double.MaxValue;

            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory story = ramStories.GetAt(i);
                if (story != null && story.dElevation < lowestElevation)
                {
                    lowestElevation = story.dElevation;
                    lowestStory = story;
                }
            }

            return lowestStory;
        }
    }

    /// <summary>
    /// Utility for filtering model layout components
    /// </summary>
    public static class ModelLayoutFilter
    {
        /// <summary>
        /// Filters levels to exclude those with elevation 0
        /// </summary>
        public static IEnumerable<Level> GetValidLevels(IEnumerable<Level> levels)
        {
            return levels.Where(level => level.Elevation != 0);
        }

        /// <summary>
        /// Filters floor types to include only those associated with valid levels
        /// </summary>
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
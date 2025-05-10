using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.ModelLayout;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    /// <summary>
    /// Helper methods for RAM model operations
    /// </summary>
    public static class RAMHelpers
    {
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

        /// <summary>
        /// Get story by UID from the RAM model
        /// </summary>
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
        /// Filters levels to exclude those with zero or negative elevation
        /// </summary>
        public static IEnumerable<Level> GetValidLevels(IEnumerable<Level> levels)
        {
            if (levels == null)
                return new List<Level>();

            return levels.Where(level => level.Elevation > 0);
        }

        /// <summary>
        /// Filters floor types to include only those associated with valid levels
        /// </summary>
        public static IEnumerable<FloorType> GetValidFloorTypes(IEnumerable<FloorType> floorTypes, IEnumerable<Level> levels)
        {
            if (floorTypes == null || levels == null)
                return new List<FloorType>();

            var validLevels = GetValidLevels(levels);
            var validLevelFloorTypeIds = validLevels
                .Where(level => !string.IsNullOrEmpty(level.FloorTypeId))
                .Select(level => level.FloorTypeId)
                .Distinct()
                .ToList();

            return floorTypes.Where(floorType =>
                !string.IsNullOrEmpty(floorType.Id) &&
                validLevelFloorTypeIds.Contains(floorType.Id));
        }
    }
}
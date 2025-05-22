using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.ModelLayout;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    // Helper methods for RAM model operations
    public static class RAMHelpers
    {
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

        // Find the lowest story in the RAM model (for foundation level)
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

    // Utility for filtering model layout components
    public static class ModelLayoutFilter
    {
        // Filters levels to exclude the lowest level by elevation
        public static IEnumerable<Level> GetValidLevels(IEnumerable<Level> levels)
        {
            if (levels == null)
                return new List<Level>();

            var levelsList = levels.ToList();
            if (levelsList.Count <= 1)
                return levelsList; // Don't filter if only one or no levels

            // Find the lowest elevation
            double lowestElevation = levelsList.Min(level => level.Elevation);

            // Return all levels except those at the lowest elevation
            return levelsList.Where(level => Math.Abs(level.Elevation - lowestElevation) > 1e-6);
        }

        // Filters floor types to include only those associated with valid levels
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
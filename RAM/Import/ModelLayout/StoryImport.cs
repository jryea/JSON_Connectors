// StoryImport.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.ModelLayout
{
    public class StoryImport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, IFloorType> _floorTypeMapping = new Dictionary<string, IFloorType>();

        public StoryImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public void SetFloorTypeMapping(IEnumerable<FloorType> floorTypes)
        {
            _floorTypeMapping.Clear();

            IFloorTypes ramFloorTypes = _model.GetFloorTypes();
            Dictionary<string, IFloorType> nameToFloorType = new Dictionary<string, IFloorType>();

            for (int i = 0; i < ramFloorTypes.GetCount(); i++)
            {
                IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                nameToFloorType[ramFloorType.strLabel] = ramFloorType;
            }

            foreach (var floorType in floorTypes)
            {
                if (!string.IsNullOrEmpty(floorType.Id) && !string.IsNullOrEmpty(floorType.Name))
                {
                    if (nameToFloorType.TryGetValue(floorType.Name, out IFloorType ramFloorType))
                    {
                        _floorTypeMapping[floorType.Id] = ramFloorType;
                    }
                }
            }
        }

        public int Import(IEnumerable<Level> allLevels)
        {
            try
            {
                int count = 0;
                IStories ramStories = _model.GetStories();

                // Calculate heights using ALL levels first
                var levelsList = allLevels.OrderBy(l => l.Elevation).ToList();
                var storyHeights = new List<(Level level, double height)>();

                Console.WriteLine("Calculating heights for all levels:");

                // Calculate heights between consecutive levels
                for (int i = 0; i < levelsList.Count; i++)
                {
                    double height;
                    if (i == 0)
                    {
                        // First level (lowest) - height from ground (0) to this level
                        height = UnitConversionUtils.ConvertToInches(levelsList[i].Elevation, _lengthUnit);
                    }
                    else
                    {
                        // Calculate height as difference from previous level
                        double currentElevation = UnitConversionUtils.ConvertToInches(levelsList[i].Elevation, _lengthUnit);
                        double previousElevation = UnitConversionUtils.ConvertToInches(levelsList[i - 1].Elevation, _lengthUnit);
                        height = currentElevation - previousElevation;
                    }

                    storyHeights.Add((levelsList[i], height));
                    Console.WriteLine($"Level {levelsList[i].Name}: elevation={levelsList[i].Elevation}, calculated height={height} inches ({height / 12:F1} feet)");
                }

                // NOW filter out the lowest level but keep the calculated heights
                var validLevels = ModelLayoutFilter.GetValidLevels(allLevels);
                var validStoryHeights = storyHeights.Where(sh => validLevels.Contains(sh.level)).ToList();

                Console.WriteLine($"After filtering: {validStoryHeights.Count} valid levels remain");
                foreach (var (level, height) in validStoryHeights)
                {
                    Console.WriteLine($"Valid level {level.Name}: will use height={height} inches ({height / 12:F1} feet)");
                }

                int storyCount = 1;
                foreach (var (level, height) in validStoryHeights)
                {
                    if (string.IsNullOrEmpty(level.Name))
                        continue;

                    string storyName = $"Story {storyCount++}";

                    int floorTypeId = 0;
                    if (!string.IsNullOrEmpty(level.FloorTypeId) &&
                        _floorTypeMapping.TryGetValue(level.FloorTypeId, out IFloorType floorType))
                    {
                        floorTypeId = floorType.lUID;
                    }
                    else
                    {
                        IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                        if (ramFloorTypes.GetCount() > 0)
                        {
                            floorTypeId = ramFloorTypes.GetAt(0).lUID;
                        }
                    }

                    if (ramStories == null)
                    {
                        Console.WriteLine("Error: ramStories is null. Skipping story creation.");
                        continue;
                    }

                    IStory story = ramStories.Add(floorTypeId, storyName, height);
                    if (story == null)
                    {
                        Console.WriteLine($"Warning: Failed to create story '{storyName}'. Skipping.");
                        continue;
                    }

                    Console.WriteLine($"Created RAM story '{storyName}' with height {height} inches ({height / 12:F1} feet) for level {level.Name}");
                    count++;
                }

                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing stories: {ex.Message}");
                throw;
            }
        }
    }
}
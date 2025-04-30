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

        public int Import(IEnumerable<Level> levels)
        {
            try
            {
                int count = 0;
                IStories ramStories = _model.GetStories();

                // Use the utility to filter valid levels
                var validLevels = ModelLayoutFilter.GetValidLevels(levels);

                double previousElevation = 0;
                List<(Level level, double height)> storyHeights = new List<(Level, double)>();

                foreach (var level in validLevels.OrderBy(l => l.Elevation))
                {
                    double elevation = UnitConversionUtils.ConvertToInches(level.Elevation, _lengthUnit);
                    double height = elevation - previousElevation;

                    storyHeights.Add((level, height));
                    previousElevation = elevation;
                }

                int storyCount = 1;
                foreach (var (level, height) in storyHeights)
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

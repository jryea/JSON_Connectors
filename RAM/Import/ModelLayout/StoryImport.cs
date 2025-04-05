// StoryImport.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.ModelLayout;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.ModelLayout
{
    // Imports levels/stories to RAM from the Core model
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

        // Sets up floor type mapping to associate floor types with RAM floor types
        public void SetFloorTypeMapping(IEnumerable<FloorType> floorTypes)
        {
            _floorTypeMapping.Clear();

            // Get RAM floor types
            IFloorTypes ramFloorTypes = _model.GetFloorTypes();
            Dictionary<string, IFloorType> nameToFloorType = new Dictionary<string, IFloorType>();

            // Create a mapping of RAM floor type names to objects
            for (int i = 0; i < ramFloorTypes.GetCount(); i++)
            {
                IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                nameToFloorType[ramFloorType.strLabel] = ramFloorType;
            }

            // Create a mapping from our model floor type ID to RAM floor type objects
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

        // Imports levels/stories to RAM

        public int Import(IEnumerable<Level> levels)
        {
            try
            {
                int count = 0;
                IStories ramStories = _model.GetStories();

                // Sort levels by elevation in ascending order
                var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

                // Calculate story heights based on the differences between elevations
                double previousElevation = 0;
                List<(Level level, double height)> storyHeights = new List<(Level, double)>();

                foreach (var level in sortedLevels)
                {
                    double elevation = Helpers.ConvertToInches(level.Elevation, _lengthUnit);
                    double height = elevation - previousElevation;

                    storyHeights.Add((level, height));
                    previousElevation = elevation;
                }

                // Create stories in RAM
                int storyCount = 1;
                foreach (var (level, height) in storyHeights)
                {
                    if (string.IsNullOrEmpty(level.Name))
                        continue;

                    // Create a story name - RAM typically uses "Story X" format
                    string storyName = $"Story {storyCount++}";

                    // Get floor type ID if available
                    int floorTypeId = 0; // Default ID
                    if (!string.IsNullOrEmpty(level.FloorTypeId) &&
                        _floorTypeMapping.TryGetValue(level.FloorTypeId, out IFloorType floorType))
                    {
                        floorTypeId = floorType.lUID;
                    }
                    else
                    {
                        // If no mapping exists, try to get the first floor type
                        IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                        if (ramFloorTypes.GetCount() > 0)
                        {
                            floorTypeId = ramFloorTypes.GetAt(0).lUID;
                        }
                    }

                    // Add the story to RAM
                    IStory story = ramStories.Add(floorTypeId, storyName, height);
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
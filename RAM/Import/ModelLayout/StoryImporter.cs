// StoryImporter.cs
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class StoryImporter : IRAMImporter<List<Level>>
    {
        private IModel _model;

        public StoryImporter(IModel model)
        {
            _model = model;
        }

        public List<Level> Import()
        {
            var levels = new List<Level>();

            try
            {
                // Get stories from RAM
                IStories stories = _model.GetStories();

                // Keep track of elevations
                double baseElevation = 0;

                for (int i = 0; i < stories.GetCount(); i++)
                {
                    IStory story = stories.GetAt(i);

                    // Calculate elevation based on height (RAM stores heights between stories)
                    baseElevation += story.dHeight / 12.0; // Convert inches to feet

                    // Create a new level
                    var level = new Level
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                        Name = story.strLabel,
                        Elevation = baseElevation,
                        // FloorTypeId will be set later after establishing relationship
                    };

                    levels.Add(level);
                }

                // If no levels were found, create a default level
                if (levels.Count == 0)
                {
                    levels.Add(new Level
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                        Name = "Level 1",
                        Elevation = 0
                    });
                }

                // Sort levels by elevation (ascending)
                levels.Sort((a, b) => a.Elevation.CompareTo(b.Elevation));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing stories: {ex.Message}");

                // Create a default level
                levels.Add(new Level
                {
                    Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                    Name = "Level 1",
                    Elevation = 0
                });
            }

            return levels;
        }
    }
}
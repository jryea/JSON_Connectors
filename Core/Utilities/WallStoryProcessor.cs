using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Geometry;

namespace Core.Utilities
{
    /// <summary>
    /// Utility for processing multi-story walls into per-story wall instances
    /// </summary>
    public static class WallStoryProcessor
    {
        /// <summary>
        /// Processes walls in the model to create per-story walls
        /// This should be called after all elements are added to the model
        /// </summary>
        /// <param name="model">The base model to process</param>
        public static void ProcessWallsByStory(BaseModel model)
        {
            if (model?.Elements?.Walls == null || model?.ModelLayout?.Levels == null)
                return;

            // Get all levels sorted by elevation
            var sortedLevels = model.ModelLayout.Levels
                .OrderBy(l => l.Elevation)
                .ToList();

            if (sortedLevels.Count < 2)
                return; // Need at least 2 levels to create stories

            var newWalls = new List<Wall>();
            var wallsToRemove = new List<Wall>();

            foreach (var wall in model.Elements.Walls)
            {
                var processedWalls = ProcessSingleWall(wall, sortedLevels);

                if (processedWalls.Count > 1)
                {
                    // Multi-story wall - replace with per-story walls
                    newWalls.AddRange(processedWalls);
                    wallsToRemove.Add(wall);
                }
                // Single-story walls remain unchanged
            }

            // Remove multi-story walls and add per-story walls
            foreach (var wallToRemove in wallsToRemove)
            {
                model.Elements.Walls.Remove(wallToRemove);
            }

            model.Elements.Walls.AddRange(newWalls);
        }

        private static List<Wall> ProcessSingleWall(Wall originalWall, List<Level> sortedLevels)
        {
            var result = new List<Wall>();

            // Find the base and top levels
            var baseLevel = sortedLevels.FirstOrDefault(l => l.Id == originalWall.BaseLevelId);
            var topLevel = sortedLevels.FirstOrDefault(l => l.Id == originalWall.TopLevelId);

            if (baseLevel == null || topLevel == null)
            {
                // Keep original wall if levels not found
                return new List<Wall> { originalWall };
            }

            if (baseLevel.Elevation >= topLevel.Elevation)
            {
                // Invalid wall - keep original
                return new List<Wall> { originalWall };
            }

            // Find all levels between base and top (inclusive)
            var relevantLevels = sortedLevels
                .Where(l => l.Elevation >= baseLevel.Elevation && l.Elevation <= topLevel.Elevation)
                .ToList();

            if (relevantLevels.Count < 2)
            {
                // Single story or invalid - keep original
                return new List<Wall> { originalWall };
            }

            // Create walls for each story
            for (int i = 0; i < relevantLevels.Count - 1; i++)
            {
                var storyBaseLevel = relevantLevels[i];
                var storyTopLevel = relevantLevels[i + 1];

                var storyWall = new Wall
                {
                    // Copy all properties from original wall
                    Points = new List<Point2D>(originalWall.Points),
                    PropertiesId = originalWall.PropertiesId,
                    PierId = originalWall.PierId,
                    SpandrelId = originalWall.SpandrelId,
                    IsLateral = originalWall.IsLateral,

                    // Set story-specific levels
                    BaseLevelId = storyBaseLevel.Id,
                    TopLevelId = storyTopLevel.Id
                };

                result.Add(storyWall);
            }

            return result;
        }
    }
}
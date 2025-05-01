using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;
using ETABS.Utilities;

namespace ETABS.Import.Elements
{
    // Imports wall elements from ETABS E2K file
    public class ETABSToWall
    {
        private readonly PointsCollector _pointsCollector;
        private readonly AreaParser _areaParser;
        private readonly Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private readonly Dictionary<string, string> _wallPropsByName = new Dictionary<string, string>();
        private Dictionary<string, string> _pierSpandrelsByName = new Dictionary<string, string>();
        private List<Level> _sortedLevels = new List<Level>();

        // Initializes a new instance of WallImport
        public ETABSToWall(PointsCollector pointsCollector, AreaParser areaParser)
        {
            _pointsCollector = pointsCollector;
            _areaParser = areaParser;
        }

        // Sets up level mapping by name
        public void SetLevels(IEnumerable<Level> levels)
        {
            _levelsByName.Clear();
            _sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

            foreach (var level in levels)
            {
                // Store both with and without "Story" prefix
                string normalizedName = level.Name;
                if (normalizedName.StartsWith("Story", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedName = normalizedName.Substring(5);
                }

                _levelsByName[$"Story{normalizedName}"] = level;
                _levelsByName[normalizedName] = level;

                // Special case for "Base" level
                if (normalizedName.Equals("Base", StringComparison.OrdinalIgnoreCase))
                {
                    _levelsByName["0"] = level;
                }
            }
        }

        // Sets up wall properties mapping by name
        public void SetWallProperties(IEnumerable<WallProperties> wallProperties)
        {
            _wallPropsByName.Clear();
            foreach (var prop in wallProperties)
            {
                _wallPropsByName[prop.Name] = prop.Id;
            }
        }

        // Sets up pier/spandrel definitions mapping by name
        public void SetPierSpandrelDefinitions(Dictionary<string, string> pierSpandrelNames)
        {
            _pierSpandrelsByName = new Dictionary<string, string>(pierSpandrelNames);
        }

        // Imports walls from E2K data to model
        public List<Wall> Import()
        {
            var walls = new List<Wall>();

            // Create a file logger
            string logPath = Path.Combine(Path.GetTempPath(), "ETABSToWallImport.log");
            using (StreamWriter logWriter = new StreamWriter(logPath, true))
            {
                logWriter.WriteLine($"-------- ETABSToWall.Import: Started at {DateTime.Now} --------");

                try
                {
                    // Process each wall in the area parser
                    foreach (var wallEntry in _areaParser.Walls)
                    {
                        string wallId = wallEntry.Key;
                        var connectivity = wallEntry.Value;

                        logWriter.WriteLine($"Processing wall ID: {wallId}");

                        // Get points from collector
                        var points = new List<Point2D>();

                        // Only use the first two points for each wall
                        for (int i = 0; i < Math.Min(2, connectivity.PointIds.Count); i++)
                        {
                            var pointId = connectivity.PointIds[i];
                            var point = _pointsCollector.GetPoint2D(pointId);
                            if (point != null)
                            {
                                points.Add(point);
                            }
                        }

                        // Skip if not enough points to form a wall (minimum 2 points)
                        if (points.Count < 2)
                        {
                            logWriter.WriteLine($"Not enough points for wall {wallId}");
                            continue;
                        }

                        // Get all assignments for this wall
                        if (!_areaParser.AreaAssignments.TryGetValue(wallId, out var wallAssignments))
                        {
                            logWriter.WriteLine($"No assignments found for wall {wallId}");
                            continue;
                        }

                        logWriter.WriteLine($"Found {wallAssignments.Count} assignments for wall {wallId}");

                        // Process each assignment for this wall
                        foreach (var assignment in wallAssignments)
                        {
                            logWriter.WriteLine($"Processing assignment for wall {wallId}, Story: {assignment.Story}");

                            // Get the story level for this assignment
                            if (!_levelsByName.TryGetValue(assignment.Story, out var currentLevel))
                            {
                                logWriter.WriteLine($"Cannot find level for story {assignment.Story}");
                                continue;
                            }

                            logWriter.WriteLine($"Found level: {currentLevel.Name}, Elevation: {currentLevel.Elevation}");

                            // Find base level (the level below this one)
                            Level baseLevel = null;
                            int currentIndex = _sortedLevels.IndexOf(currentLevel);
                            if (currentIndex > 0)
                            {
                                baseLevel = _sortedLevels[currentIndex - 1];
                                logWriter.WriteLine($"Found base level: {baseLevel.Name}, Elevation: {baseLevel.Elevation}");
                            }
                            else
                            {
                                baseLevel = currentLevel; // Use same level if it's the lowest
                                logWriter.WriteLine($"Using current level as base level (lowest level)");
                            }

                            // Get wall properties if available
                            string wallPropId = null;
                            if (!string.IsNullOrEmpty(assignment.Section) &&
                                _wallPropsByName.TryGetValue(assignment.Section, out var propId))
                            {
                                wallPropId = propId;
                                logWriter.WriteLine($"Found wall property ID: {wallPropId} for section: {assignment.Section}");
                            }
                            else
                            {
                                logWriter.WriteLine($"Could not find wall property for section: {assignment.Section}");
                            }

                            // Create wall object for this assignment
                            var wall = new Wall
                            {
                                Id = IdGenerator.Generate(IdGenerator.Elements.WALL),
                                Points = new List<Point2D>(points), // Create new list to avoid shared references
                                BaseLevelId = baseLevel?.Id,
                                TopLevelId = currentLevel?.Id,
                                PropertiesId = wallPropId
                            };

                            // Add to the list
                            walls.Add(wall);
                            logWriter.WriteLine($"Added wall: {wall.Id}, BaseLevelId: {wall.BaseLevelId}, TopLevelId: {wall.TopLevelId}");
                        }
                    }

                    logWriter.WriteLine($"Total walls created: {walls.Count}");
                }

                catch (Exception ex)
                {
                    logWriter.WriteLine($"Exception occurred: {ex.Message}");
                    logWriter.WriteLine($"Stack trace: {ex.StackTrace}");
                }

                logWriter.WriteLine($"-------- ETABSToWall.Import: Completed at {DateTime.Now} --------");
                logWriter.Flush();
            }

            return walls;
        }
    }
}
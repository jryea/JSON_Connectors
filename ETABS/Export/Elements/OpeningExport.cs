using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Utilities;
using ETABS.Utilities;

namespace ETABS.Export.Elements
{
    // Imports opening elements from ETABS E2K file
    public class OpeningExport
    {
        private readonly PointsCollector _pointsCollector;
        private readonly AreaParser _areaParser;
        private readonly Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private readonly Dictionary<string, Floor> _floorsByLevelId = new Dictionary<string, Floor>();    

        // Initializes a new instance of OpeningExport
        public OpeningExport(PointsCollector pointsCollector, AreaParser areaParser)
        {
            _pointsCollector = pointsCollector;
            _areaParser = areaParser;
        }

        // Sets up level mapping by name
        public void SetLevels(IEnumerable<Level> levels)
        {
            _levelsByName.Clear();
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
        public void SetFloors(IEnumerable<Floor> floors)
        {
            _floorsByLevelId.Clear();
            foreach (var floor in floors)
            {
                if (!string.IsNullOrEmpty(floor.LevelId))
                {
                    _floorsByLevelId[floor.LevelId] = floor;
                }
            }
        }

        // Imports openings from E2K data to model
        public List<Opening> Export()
        {
            var openings = new List<Opening>();

            // Process each opening in the area parser (areas marked as openings)
            foreach (var openingEntry in _areaParser.Openings)
            {
                string openingId = openingEntry.Key;
                var connectivity = openingEntry.Value;

                // Get points from collector
                var points = new List<Point2D>();
                foreach (var pointId in connectivity.PointIds)
                {
                    var point = _pointsCollector.GetPoint2D(pointId);
                    if (point != null)
                    {
                        points.Add(point);
                    }
                }

                // Skip if not enough points to form an opening
                if (points.Count < 3)
                    continue;

                // Check assignments for this opening to find the story
                if (_areaParser.AreaAssignments.TryGetValue(openingId, out var assignments))
                {
                    foreach (var assignment in assignments)
                    {
                        // Get level from story name
                        if (_levelsByName.TryGetValue(assignment.Story, out var level))
                        {
                            // Find floor on this level
                            string floorId = null;
                            if (_floorsByLevelId.TryGetValue(level.Id, out var floorOnLevel))
                            {
                                floorId = floorOnLevel.Id;
                            }

                            // Create opening object
                            var opening = new Opening
                            {
                                Id = IdGenerator.Generate(IdGenerator.Elements.OPENING),
                                Points = new List<Point2D>(points),
                                FloorId = floorId
                            };

                            openings.Add(opening);
                        }
                    }
                }
                else
                {
                    // If no assignments found, create an opening with minimal data
                    var opening = new Opening
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.OPENING),
                        Points = new List<Point2D>(points)
                    };

                    openings.Add(opening);
                }
            }

            return openings;
        }
    }
}
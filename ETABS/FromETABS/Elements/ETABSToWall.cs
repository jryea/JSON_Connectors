using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;
using ETABS.Import.Utilities;

namespace ETABS.Import.Elements
{
    // Imports wall elements from ETABS E2K file
    public class ETABSToWall
    {
        private PointsCollector _pointsCollector;
        private AreaParser _areaParser;
        private Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private Dictionary<string, string> _wallPropsByName = new Dictionary<string, string>();
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
            var walls = new Dictionary<string, Wall>(); // Use dictionary to prevent duplicates

            // Process each wall in the area parser
            foreach (var wallEntry in _areaParser.Walls)
            {
                string wallId = wallEntry.Key;
                var connectivity = wallEntry.Value;

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

                // Skip if not enough points to form a wall (minimum 2 points)
                if (points.Count < 2)
                    continue;

                // Create wall object with basic information
                Wall wall = new Wall
                {
                    Id = IdGenerator.Generate(IdGenerator.Elements.WALL),
                    Points = points
                };

                // Check for assignments to determine properties and levels
                if (_areaParser.AreaAssignments.TryGetValue(wallId, out var assignments) &&
                    assignments.Count > 0)
                {
                    // Get wall properties from first assignment with a section
                    var firstAssignmentWithSection = assignments.FirstOrDefault(a => !string.IsNullOrEmpty(a.Section));
                    if (firstAssignmentWithSection != null &&
                        _wallPropsByName.TryGetValue(firstAssignmentWithSection.Section, out var propId))
                    {
                        wall.PropertiesId = propId;
                    }

                    // Get pier/spandrel information if available
                    // This would require additional parsing of PIER/SPANDREL section in E2K
                    // For this example, we'll assume it's set separately

                    // Determine base and top levels from assignments
                    if (assignments.Count > 0)
                    {
                        // Sort assignments by story (which requires converting to levels)
                        var sortedAssignments = new List<Tuple<AreaParser.AreaAssignment, Level>>();
                        foreach (var assignment in assignments)
                        {
                            if (_levelsByName.TryGetValue(assignment.Story, out var level))
                            {
                                sortedAssignments.Add(new Tuple<AreaParser.AreaAssignment, Level>(assignment, level));
                            }
                        }

                        if (sortedAssignments.Count > 0)
                        {
                            // Sort by elevation
                            sortedAssignments.Sort((a, b) => a.Item2.Elevation.CompareTo(b.Item2.Elevation));

                            // Lowest level is base
                            wall.BaseLevelId = sortedAssignments[0].Item2.Id;

                            // Highest level is top
                            wall.TopLevelId = sortedAssignments[sortedAssignments.Count - 1].Item2.Id;
                        }
                    }
                }

                // Add to dictionary
                walls[wall.Id] = wall;
            }

            return walls.Values.ToList();
        }
    }
}
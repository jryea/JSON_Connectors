using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;
using ETABS.Import.Utilities;

namespace ETABS.Import.Elements
{
    /// <summary>
    /// Imports column elements from ETABS E2K file
    /// </summary>
    public class ETABSToColumn
    {
        private readonly PointsCollector _pointsCollector;
        private readonly LineConnectivityParser _connectivityParser;
        private readonly LineAssignmentParser _assignmentParser;
        private readonly Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private readonly Dictionary<string, string> _framePropsByName = new Dictionary<string, string>();
        private List<Level> _sortedLevels = new List<Level>();

        /// <summary>
        /// Initializes a new instance of ColumnImport
        /// </summary>
        /// <param name="pointsCollector">Points collector for coordinate data</param>
        /// <param name="connectivityParser">Line connectivity parser</param>
        /// <param name="assignmentParser">Line assignment parser</param>
        public ETABSToColumn(
            PointsCollector pointsCollector,
            LineConnectivityParser connectivityParser,
            LineAssignmentParser assignmentParser)
        {
            _pointsCollector = pointsCollector;
            _connectivityParser = connectivityParser;
            _assignmentParser = assignmentParser;
        }

        /// <summary>
        /// Sets up level mapping by name
        /// </summary>
        /// <param name="levels">Collection of levels in the model</param>
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

        /// <summary>
        /// Sets up frame properties mapping by name
        /// </summary>
        /// <param name="frameProperties">Collection of frame properties in the model</param>
        public void SetFrameProperties(IEnumerable<FrameProperties> frameProperties)
        {
            _framePropsByName.Clear();
            foreach (var prop in frameProperties)
            {
                _framePropsByName[prop.Name] = prop.Id;
            }
        }

        /// <summary>
        /// Imports columns from E2K data to model
        /// </summary>
        /// <returns>Collection of imported column elements</returns>
        // Modified Import method for ColumnImport class
        public List<Column> Import()
        {
            var columns = new List<Column>();

            // Process each column in the connectivity parser
            foreach (var columnEntry in _connectivityParser.Columns)
            {
                string columnId = columnEntry.Key;
                var connectivity = columnEntry.Value;

                // Get points from collector
                Point2D startPoint = _pointsCollector.GetPoint2D(connectivity.Point1Id);
                Point2D endPoint = _pointsCollector.GetPoint2D(connectivity.Point2Id);

                // Skip if points not found
                if (startPoint == null || endPoint == null)
                    continue;

                // Find all assignments for this column ID
                var storyAssignments = _assignmentParser.LineAssignments
                    .Where(a => a.Key == columnId)
                    .Select(a => a.Value)
                    .ToList();

                // If no assignments found, create a single column with minimal data
                if (storyAssignments.Count == 0)
                {
                    var column = new Column
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN),
                        StartPoint = startPoint,
                        EndPoint = endPoint
                    };
                    columns.Add(column);
                    continue;
                }

                // Create a column for each story assignment
                foreach (var assignment in storyAssignments)
                {
                    string framePropId = null;
                    string storyName = assignment.Story;

                    // Look up frame properties ID from section name
                    if (_framePropsByName.TryGetValue(assignment.Section, out var propId))
                    {
                        framePropId = propId;
                    }

                    // Find current level from story name
                    Level currentLevel = null;
                    if (_levelsByName.TryGetValue(storyName, out var level))
                    {
                        currentLevel = level;
                    }
                    else
                    {
                        continue; // Skip if level not found
                    }

                    // Find the level below (for base level)
                    Level baseLevel = null;
                    int currentIndex = _sortedLevels.IndexOf(currentLevel);
                    if (currentIndex > 0)
                    {
                        baseLevel = _sortedLevels[currentIndex - 1];
                    }
                    else
                    {
                        baseLevel = currentLevel; // Use same level if it's the lowest
                    }

                    // Create column object for this story
                    var column = new Column
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN),
                        StartPoint = startPoint,
                        EndPoint = endPoint,
                        BaseLevelId = baseLevel?.Id,
                        TopLevelId = currentLevel?.Id,
                        FramePropertiesId = framePropId,
                        IsLateral = assignment.IsLateral
                    };

                    columns.Add(column);
                }
            }

            return columns;
        }
    }
}
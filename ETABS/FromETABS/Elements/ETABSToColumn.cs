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

                // Get level and section from assignments
                string framePropId = null;
                bool isLateral = false;
                string storyName = null;

                if (_assignmentParser.LineAssignments.TryGetValue(columnId, out var assignment))
                {
                    storyName = assignment.Story;

                    // Look up frame properties ID from section name
                    if (_framePropsByName.TryGetValue(assignment.Section, out var propId))
                    {
                        framePropId = propId;
                    }

                    isLateral = assignment.IsLateral;
                }

                // Determine base and top levels
                string baseLevelId = null;
                string topLevelId = null;

                // For columns, ETABS typically assigns them to the top story they belong to
                if (storyName != null && _levelsByName.TryGetValue(storyName, out var topLevel))
                {
                    topLevelId = topLevel.Id;

                    // Find the level below the assigned level
                    int topLevelIndex = _sortedLevels.IndexOf(topLevel);
                    if (topLevelIndex > 0)
                    {
                        baseLevelId = _sortedLevels[topLevelIndex - 1].Id;
                    }
                    else
                    {
                        baseLevelId = topLevel.Id; // If it's the lowest level, use the same level as base
                    }
                }

                // Create column object
                var column = new Column
                {
                    Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN),
                    StartPoint = startPoint,
                    EndPoint = endPoint,
                    BaseLevelId = baseLevelId,
                    TopLevelId = topLevelId,
                    FramePropertiesId = framePropId,
                    IsLateral = isLateral
                };

                columns.Add(column);
            }

            return columns;
        }
    }
}
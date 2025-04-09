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

        // Sets up frame properties mapping by name
        public void SetFrameProperties(IEnumerable<FrameProperties> frameProperties)
        {
            _framePropsByName.Clear();
            foreach (var prop in frameProperties)
            {
                _framePropsByName[prop.Name] = prop.Id;
            }
        }

        // Imports columns from E2K data to model
     
        public List<Column> Import()
        {
            var columns = new List<Column>();

            // Get all line assignments for columns
            var columnAssignments = _assignmentParser.LineAssignments
                .Where(kvp => _connectivityParser.Columns.ContainsKey(kvp.Key))
                .ToList();

            // Process each column assignment directly instead of by connectivity
            foreach (var assignmentEntry in columnAssignments)
            {
                string columnId = assignmentEntry.Key;
                var assignment = assignmentEntry.Value;

                // Only process if we can find the connectivity
                if (!_connectivityParser.Columns.TryGetValue(columnId, out var connectivity))
                    continue;

                // Get points from collector
                Point2D startPoint = _pointsCollector.GetPoint2D(connectivity.Point1Id);
                Point2D endPoint = _pointsCollector.GetPoint2D(connectivity.Point2Id);

                // Skip if points not found
                if (startPoint == null || endPoint == null)
                    continue;

                // Get properties from assignment
                string framePropId = null;
                if (_framePropsByName.TryGetValue(assignment.Section, out var propId))
                {
                    framePropId = propId;
                }

                // Find current level from story name
                Level currentLevel = null;
                if (_levelsByName.TryGetValue(assignment.Story, out var level))
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

                // Create column object for this specific assignment
                var column = new Column
                {
                    Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN),
                    StartPoint = startPoint,
                    EndPoint = endPoint,
                    BaseLevelId = baseLevel?.Id,
                    TopLevelId = currentLevel?.Id,
                    FramePropertiesId = framePropId
                };

                columns.Add(column);
            }

            return columns;
        }
    }
}
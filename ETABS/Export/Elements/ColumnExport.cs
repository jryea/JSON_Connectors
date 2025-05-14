using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;
using ETABS.Utilities;

namespace ETABS.Export.Elements
{
    // Imports column elements from ETABS E2K file
    public class ColumnExport
    {
        private readonly PointsCollector _pointsCollector;
        private readonly LineConnectivityParser _connectivityParser;
        private readonly LineAssignmentParser _assignmentParser;
        private readonly Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private readonly Dictionary<string, string> _framePropsByName = new Dictionary<string, string>();
        private List<Level> _sortedLevels = new List<Level>();

        // Initializes a new instance of ColumnImport
        public ColumnExport(
            PointsCollector pointsCollector,
            LineConnectivityParser connectivityParser,
            LineAssignmentParser assignmentParser)
        {
            _pointsCollector = pointsCollector;
            _connectivityParser = connectivityParser;
            _assignmentParser = assignmentParser;
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
        // In ColumnExport.cs - removing isLateral references
        public List<Column> Export()
        {
            var columns = new List<Column>();

            // Create a file logger
            string logPath = Path.Combine(Path.GetTempPath(), "ETABSToColumnImport.log");
            using (StreamWriter logWriter = new StreamWriter(logPath, true))
            {
                logWriter.WriteLine($"-------- ETABSToColumn.Import: Started at {DateTime.Now} --------");

                try
                {
                    // Get ALL line assignments from the assignment parser
                    var allLineAssignments = _assignmentParser.LineAssignments;
                    logWriter.WriteLine($"Total line assignments found: {allLineAssignments.Count}");

                    // Get all column IDs from connectivity parser
                    var columnIds = _connectivityParser.Columns.Keys.ToList();

                    // Process each column ID
                    foreach (var columnId in columnIds)
                    {
                        logWriter.WriteLine($"Processing column ID: {columnId}");

                        // Get the connectivity for this column
                        var connectivity = _connectivityParser.Columns[columnId];

                        // Get points from collector
                        Point2D startPoint = _pointsCollector.GetPoint2D(connectivity.Point1Id);
                        Point2D endPoint = _pointsCollector.GetPoint2D(connectivity.Point2Id);

                        if (startPoint == null || endPoint == null)
                        {
                            logWriter.WriteLine($"Cannot find points for column {columnId}");
                            continue;
                        }

                        // Get all assignments for this column
                        if (!allLineAssignments.TryGetValue(columnId, out var columnAssignments))
                        {
                            logWriter.WriteLine($"No assignments found for column {columnId}");
                            continue;
                        }

                        logWriter.WriteLine($"Found {columnAssignments.Count} assignments for column {columnId}");

                        // Process each assignment for this column
                        foreach (var assignment in columnAssignments)
                        {
                            logWriter.WriteLine($"Processing assignment for column {columnId}, Story: {assignment.Story}");

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

                            // Get frame properties if available
                            string framePropId = null;
                            if (!string.IsNullOrEmpty(assignment.Section) &&
                                _framePropsByName.TryGetValue(assignment.Section, out var propId))
                            {
                                framePropId = propId;
                                logWriter.WriteLine($"Found frame property ID: {framePropId} for section: {assignment.Section}");
                            }
                            else
                            {
                                logWriter.WriteLine($"Could not find frame property for section: {assignment.Section}");
                            }

                            // Create column object for this assignment
                            var column = new Column
                            {
                                Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN),
                                StartPoint = startPoint,
                                EndPoint = endPoint,
                                BaseLevelId = baseLevel?.Id,
                                TopLevelId = currentLevel?.Id,
                                FramePropertiesId = framePropId
                                // IsLateral property removed - will use default value from model class
                            };

                            // Add to the list
                            columns.Add(column);
                            logWriter.WriteLine($"Added column: {column.Id}, BaseLevelId: {column.BaseLevelId}, TopLevelId: {column.TopLevelId}");
                        }
                    }

                    logWriter.WriteLine($"Total columns created: {columns.Count}");
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine($"Exception occurred: {ex.Message}");
                    logWriter.WriteLine($"Stack trace: {ex.StackTrace}");
                }

                logWriter.WriteLine($"-------- ETABSToColumn.Import: Completed at {DateTime.Now} --------");
                logWriter.Flush();
            }

            return columns;
        }
    }
}
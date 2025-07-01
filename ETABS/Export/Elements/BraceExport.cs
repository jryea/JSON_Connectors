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

namespace ETABS.Export.Elements
{
    // Imports brace elements from ETABS E2K file

    public class BraceExport
    {
        private readonly PointsCollector _pointsCollector;
        private readonly LineConnectivityParser _connectivityParser;
        private readonly LineAssignmentParser _assignmentParser;
        private readonly Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private readonly Dictionary<string, string> _framePropsByName = new Dictionary<string, string>();
        private List<Level> _sortedLevels = new List<Level>();

        // Initializes a new instance of BraceImport
        
        public BraceExport(
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

        // Imports braces from E2K data to model
        public List<Brace> Export()
        {
            var braces = new List<Brace>();

            // Create a file logger
            string logPath = Path.Combine(Path.GetTempPath(), "ETABSToBraceImport.log");
            using (StreamWriter logWriter = new StreamWriter(logPath, true))
            {
                logWriter.WriteLine($"-------- ETABSToBrace.Import: Started at {DateTime.Now} --------");

                try
                {
                    // Get ALL line assignments from the assignment parser
                    var allLineAssignments = _assignmentParser.LineAssignments;
                    logWriter.WriteLine($"Total line assignments found: {allLineAssignments.Count}");

                    // Get all brace IDs from connectivity parser
                    var braceIds = _connectivityParser.Braces.Keys.ToList();

                    // Process each brace ID
                    foreach (var braceId in braceIds)
                    {
                        logWriter.WriteLine($"Processing brace ID: {braceId}");

                        // Get the connectivity for this brace
                        var connectivity = _connectivityParser.Braces[braceId];

                        // Get points from collector
                        Point2D startPoint = _pointsCollector.GetPoint2D(connectivity.Point1Id);
                        Point2D endPoint = _pointsCollector.GetPoint2D(connectivity.Point2Id);

                        if (startPoint == null || endPoint == null)
                        {
                            logWriter.WriteLine($"Cannot find points for brace {braceId}");
                            continue;
                        }

                        // Get all assignments for this brace
                        if (!allLineAssignments.TryGetValue(braceId, out var braceAssignments))
                        {
                            logWriter.WriteLine($"No assignments found for brace {braceId}");
                            continue;
                        }

                        logWriter.WriteLine($"Found {braceAssignments.Count} assignments for brace {braceId}");

                        // Process each assignment for this brace
                        foreach (var assignment in braceAssignments)
                        {
                            logWriter.WriteLine($"Processing assignment for brace {braceId}, Story: {assignment.Story}");

                            // Get the story level for this assignment
                            if (!_levelsByName.TryGetValue(assignment.Story, out var currentLevel))
                            {
                                logWriter.WriteLine($"Cannot find level for story {assignment.Story}");
                                continue;
                            }

                            logWriter.WriteLine($"Found level: {currentLevel.Name}, Elevation: {currentLevel.Elevation}");

                            // Find the level below this one for the base level
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

                            // Create brace object for this assignment
                            var brace = new Brace
                            {
                                Id = IdGenerator.Generate(IdGenerator.Elements.BRACE),
                                StartPoint = startPoint,
                                EndPoint = endPoint,
                                BaseLevelId = baseLevel?.Id,
                                TopLevelId = currentLevel?.Id,
                                FramePropertiesId = framePropId
                            };

                            // Add to the list
                            braces.Add(brace);
                            logWriter.WriteLine($"Added brace: {brace.Id}, BaseLevelId: {brace.BaseLevelId}, TopLevelId: {brace.TopLevelId}");
                        }
                    }

                    logWriter.WriteLine($"Total braces created: {braces.Count}");
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine($"Exception occurred: {ex.Message}");
                    logWriter.WriteLine($"Stack trace: {ex.StackTrace}");
                }

                logWriter.WriteLine($"-------- ETABSToBrace.Import: Completed at {DateTime.Now} --------");
                logWriter.Flush();
            }

            return braces;
        }
    }
}
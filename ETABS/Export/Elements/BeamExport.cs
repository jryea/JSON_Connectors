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
    public class BeamExport
    {
        private readonly PointsCollector _pointsCollector;
        private readonly LineConnectivityParser _connectivityParser;
        private readonly LineAssignmentParser _assignmentParser;
        private readonly Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private readonly Dictionary<string, string> _framePropsByName = new Dictionary<string, string>();

        public BeamExport(
            PointsCollector pointsCollector,
            LineConnectivityParser connectivityParser,
            LineAssignmentParser assignmentParser)
        {
            _pointsCollector = pointsCollector;
            _connectivityParser = connectivityParser;
            _assignmentParser = assignmentParser;
        }

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

        // Exports beams from E2K data to model
        public List<Beam> Export()
        {
            var beams = new List<Beam>();

            // Create a file logger
            string logPath = Path.Combine(Path.GetTempPath(), "ETABSToBeamImport.log");
            using (StreamWriter logWriter = new StreamWriter(logPath, true))
            {
                logWriter.WriteLine($"-------- ETABSToBeam.Import: Started at {DateTime.Now} --------");

                try
                {
                    // Get ALL line assignments from the assignment parser
                    var allLineAssignments = _assignmentParser.LineAssignments;
                    logWriter.WriteLine($"Total line assignments found: {allLineAssignments.Count}");

                    // Get all beam IDs from connectivity parser
                    var beamIds = _connectivityParser.Beams.Keys.ToList();

                    // Process each beam ID
                    foreach (var beamId in beamIds)
                    {
                        logWriter.WriteLine($"Processing beam ID: {beamId}");

                        // Get the connectivity for this beam
                        var connectivity = _connectivityParser.Beams[beamId];

                        // Get points from collector
                        Point2D startPoint = _pointsCollector.GetPoint2D(connectivity.Point1Id);
                        Point2D endPoint = _pointsCollector.GetPoint2D(connectivity.Point2Id);

                        if (startPoint == null || endPoint == null)
                        {
                            logWriter.WriteLine($"Cannot find points for beam {beamId}");
                            continue;
                        }

                        // Get all assignments for this beam
                        if (!allLineAssignments.TryGetValue(beamId, out var beamAssignments))
                        {
                            logWriter.WriteLine($"No assignments found for beam {beamId}");
                            continue;
                        }

                        logWriter.WriteLine($"Found {beamAssignments.Count} assignments for beam {beamId}");

                        // Process each assignment for this beam
                        foreach (var assignment in beamAssignments)
                        {
                            logWriter.WriteLine($"Processing assignment for beam {beamId}, Story: {assignment.Story}");

                            // Get the story level for this assignment
                            if (!_levelsByName.TryGetValue(assignment.Story, out var level))
                            {
                                logWriter.WriteLine($"Cannot find level for story {assignment.Story}");
                                continue;
                            }

                            logWriter.WriteLine($"Found level: {level.Name}, Elevation: {level.Elevation}");

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

                            // Create beam object for this assignment
                            var beam = new Beam
                            {
                                Id = IdGenerator.Generate(IdGenerator.Elements.BEAM),
                                StartPoint = startPoint,
                                EndPoint = endPoint,
                                LevelId = level?.Id,
                                FramePropertiesId = framePropId,
                                IsJoist = DetermineIfJoist(assignment)
                                // IsLateral property removed - will use default value from model class
                            };

                            // Add to the list
                            beams.Add(beam);
                            logWriter.WriteLine($"Added beam: {beam.Id}, LevelId: {beam.LevelId}, IsJoist: {beam.IsJoist}");
                        }
                    }

                    logWriter.WriteLine($"Total beams created: {beams.Count}");
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine($"Exception occurred: {ex.Message}");
                    logWriter.WriteLine($"Stack trace: {ex.StackTrace}");
                }

                logWriter.WriteLine($"-------- ETABSToBeam.Import: Completed at {DateTime.Now} --------");
                logWriter.Flush();
            }

            return beams;
        }

        // Helper method to determine if a beam is a joist based on assignment properties
        private bool DetermineIfJoist(LineAssignmentParser.LineAssignment assignment)
        {
            // Check for release conditions typical of joists (pinned ends)
            if (!string.IsNullOrEmpty(assignment.ReleaseCondition) &&
                (assignment.ReleaseCondition.Contains("M2I M2J M3I M3J") ||
                 assignment.ReleaseCondition.Contains("PINNED")))
            {
                return true;
            }

            // Or check section name for keywords indicating a joist
            if (!string.IsNullOrEmpty(assignment.Section) &&
                (assignment.Section.ToLower().Contains("joist") ||
                 assignment.Section.ToLower().Contains("comp")))
            {
                return true;
            }

            return false;
        }
    }
}
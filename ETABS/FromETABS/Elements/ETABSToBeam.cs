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
    // Imports beam elements from ETABS E2K file
    public class ETABSToBeam
    {
        private readonly PointsCollector _pointsCollector;
        private readonly LineConnectivityParser _connectivityParser;
        private readonly LineAssignmentParser _assignmentParser;
        private readonly Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private readonly Dictionary<string, string> _framePropsByName = new Dictionary<string, string>();

        // Initializes a new instance of BeamImport
        public ETABSToBeam(
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

        // Imports beams from E2K data to model
        public List<Beam> Import()
        {
            var beams = new List<Beam>();

            // Process each beam in the connectivity parser
            foreach (var beamEntry in _connectivityParser.Beams)
            {
                string beamId = beamEntry.Key;
                var connectivity = beamEntry.Value;

                // Get points from collector
                Point2D startPoint = _pointsCollector.GetPoint2D(connectivity.Point1Id);
                Point2D endPoint = _pointsCollector.GetPoint2D(connectivity.Point2Id);

                // Skip if points not found
                if (startPoint == null || endPoint == null)
                    continue;

                // Find all assignments for this beam ID
                var storyAssignments = _assignmentParser.LineAssignments
                    .Where(a => a.Key == beamId)
                    .Select(a => a.Value)
                    .ToList();

                // If no assignments found, create a single beam with minimal data
                if (storyAssignments.Count == 0)
                {
                    var beam = new Beam
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.BEAM),
                        StartPoint = startPoint,
                        EndPoint = endPoint
                    };
                    beams.Add(beam);
                    continue;
                }

                // Create a beam for each story assignment
                foreach (var assignment in storyAssignments)
                {
                    string framePropId = null;
                    string storyName = assignment.Story;

                    // Look up frame properties ID from section name
                    if (_framePropsByName.TryGetValue(assignment.Section, out var propId))
                    {
                        framePropId = propId;
                    }

                    // Find level from story name
                    Level level = null;
                    if (_levelsByName.TryGetValue(storyName, out var foundLevel))
                    {
                        level = foundLevel;
                    }
                    else
                    {
                        continue; // Skip if level not found
                    }

                    // Create beam object for this story
                    var beam = new Beam
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.BEAM),
                        StartPoint = startPoint,
                        EndPoint = endPoint,
                        LevelId = level?.Id,
                        FramePropertiesId = framePropId,
                        IsLateral = assignment.IsLateral
                    };

                    // If beam is a joist (can be determined from release conditions or section type)
                    beam.IsJoist = DetermineIfJoist(assignment);

                    beams.Add(beam);
                }
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
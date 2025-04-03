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

                // Get level and section from assignments
                string levelId = null;
                string framePropId = null;
                bool isLateral = false;

                if (_assignmentParser.LineAssignments.TryGetValue(beamId, out var assignment))
                {
                    // Look up level ID from story name
                    if (_levelsByName.TryGetValue(assignment.Story, out var level))
                    {
                        levelId = level.Id;
                    }

                    // Look up frame properties ID from section name
                    if (_framePropsByName.TryGetValue(assignment.Section, out var propId))
                    {
                        framePropId = propId;
                    }

                    isLateral = assignment.IsLateral;
                }

                // Create beam object
                var beam = new Beam
                {
                    Id = IdGenerator.Generate(IdGenerator.Elements.BEAM),
                    StartPoint = startPoint,
                    EndPoint = endPoint,
                    LevelId = levelId,
                    FramePropertiesId = framePropId,
                    IsLateral = isLateral
                };

                beams.Add(beam);
            }

            return beams;
        }
    }
}
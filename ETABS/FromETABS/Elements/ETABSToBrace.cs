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
    // Imports brace elements from ETABS E2K file

    public class ETABSToBrace
    {
        private readonly ETABSToPoints _pointsCollector;
        private readonly LineConnectivityParser _connectivityParser;
        private readonly LineAssignmentParser _assignmentParser;
        private readonly Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private readonly Dictionary<string, string> _framePropsByName = new Dictionary<string, string>();
        private List<Level> _sortedLevels = new List<Level>();

        // Initializes a new instance of BraceImport
        
        public ETABSToBrace(
            ETABSToPoints pointsCollector,
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

        // Imports braces from E2K data to model
        public List<Brace> Import()
        {
            var braces = new List<Brace>();

            // Process each brace in the connectivity parser
            foreach (var braceEntry in _connectivityParser.Braces)
            {
                string braceId = braceEntry.Key;
                var connectivity = braceEntry.Value;

                // Get points from collector
                Point2D startPoint = _pointsCollector.GetPoint2D(connectivity.Point1Id);
                Point2D endPoint = _pointsCollector.GetPoint2D(connectivity.Point2Id);

                // Skip if points not found
                if (startPoint == null || endPoint == null)
                    continue;

                // Get frame properties and story from assignments
                string framePropId = null;
                string storyName = null;

                if (_assignmentParser.LineAssignments.TryGetValue(braceId, out var assignment))
                {
                    storyName = assignment.Story;

                    // Look up frame properties ID from section name
                    if (_framePropsByName.TryGetValue(assignment.Section, out var propId))
                    {
                        framePropId = propId;
                    }
                }

                // Determine base and top levels
                string baseLevelId = null;
                string topLevelId = null;

                // For braces, ETABS typically assigns them to each story they span
                if (storyName != null && _levelsByName.TryGetValue(storyName, out var level))
                {
                    // For simplicity, we'll use the assigned level as the top level
                    topLevelId = level.Id;

                    // Find the level below the assigned level
                    int levelIndex = _sortedLevels.IndexOf(level);
                    if (levelIndex > 0)
                    {
                        baseLevelId = _sortedLevels[levelIndex - 1].Id;
                    }
                    else
                    {
                        baseLevelId = level.Id; // If it's the lowest level, use the same level as base
                    }
                }

                // Create brace object
                var brace = new Brace
                {
                    Id = IdGenerator.Generate(IdGenerator.Elements.BRACE),
                    StartPoint = startPoint,
                    EndPoint = endPoint,
                    BaseLevelId = baseLevelId,
                    TopLevelId = topLevelId,
                    FramePropertiesId = framePropId
                };

                braces.Add(brace);
            }

            return braces;
        }
    }
}
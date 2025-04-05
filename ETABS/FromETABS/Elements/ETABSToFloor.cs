using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;
using ETABS.Import.Utilities;

namespace ETABS.Import.Elements
{
    /// <summary>
    /// Imports floor elements from ETABS E2K file
    /// </summary>
    public class ETABSToFloor
    {
        private readonly PointsCollector _pointsCollector;
        private readonly AreaParser _areaParser;
        private readonly Dictionary<string, Level> _levelsByName = new Dictionary<string, Level>();
        private readonly Dictionary<string, string> _floorPropsByName = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _diaphragmsByName = new Dictionary<string, string>();

        /// <summary>
        /// Initializes a new instance of FloorImport
        /// </summary>
        /// <param name="pointsCollector">Points collector for coordinate data</param>
        /// <param name="areaParser">Area parser for floors and walls</param>
        public ETABSToFloor(PointsCollector pointsCollector, AreaParser areaParser)
        {
            _pointsCollector = pointsCollector;
            _areaParser = areaParser;
        }

        /// <summary>
        /// Sets up level mapping by name
        /// </summary>
        /// <param name="levels">Collection of levels in the model</param>
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

                // Special case for "Base" level
                if (normalizedName.Equals("Base", StringComparison.OrdinalIgnoreCase))
                {
                    _levelsByName["0"] = level;
                }
            }
        }

        /// <summary>
        /// Sets up floor properties mapping by name
        /// </summary>
        /// <param name="floorProperties">Collection of floor properties in the model</param>
        public void SetFloorProperties(IEnumerable<FloorProperties> floorProperties)
        {
            _floorPropsByName.Clear();
            foreach (var prop in floorProperties)
            {
                _floorPropsByName[prop.Name] = prop.Id;
            }
        }

        /// <summary>
        /// Sets up diaphragm mapping by name
        /// </summary>
        /// <param name="diaphragms">Collection of diaphragms in the model</param>
        public void SetDiaphragms(IEnumerable<Diaphragm> diaphragms)
        {
            _diaphragmsByName.Clear();
            foreach (var diaphragm in diaphragms)
            {
                _diaphragmsByName[diaphragm.Name] = diaphragm.Id;
            }
        }

        /// <summary>
        /// Imports floors from E2K data to model
        /// </summary>
        /// <returns>Collection of imported floor elements</returns>
        public List<Floor> Import()
        {
            var floors = new List<Floor>();

            // Process each floor in the area parser
            foreach (var floorEntry in _areaParser.Floors)
            {
                string floorId = floorEntry.Key;
                var connectivity = floorEntry.Value;

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

                // Skip if not enough points to form a floor
                if (points.Count < 3)
                    continue;

                // Check all assignments for this floor (potentially multiple stories)
                if (_areaParser.AreaAssignments.TryGetValue(floorId, out var assignments))
                {
                    foreach (var assignment in assignments)
                    {
                        // Get level ID from story name
                        string levelId = null;
                        if (_levelsByName.TryGetValue(assignment.Story, out var level))
                        {
                            levelId = level.Id;
                        }
                        else
                        {
                            continue; // Skip if level not found
                        }

                        // Get floor properties ID from section name
                        string floorPropsId = null;
                        if (!string.IsNullOrEmpty(assignment.Section) &&
                            _floorPropsByName.TryGetValue(assignment.Section, out var propId))
                        {
                            floorPropsId = propId;
                        }

                        // Get diaphragm ID
                        string diaphragmId = null;
                        if (!string.IsNullOrEmpty(assignment.DiaphragmId) &&
                            _diaphragmsByName.TryGetValue(assignment.DiaphragmId, out var diaId))
                        {
                            diaphragmId = diaId;
                        }

                        // Create floor object
                        var floor = new Floor
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR),
                            LevelId = levelId,
                            FloorPropertiesId = floorPropsId,
                            Points = new List<Point2D>(points), // Create a new list to avoid shared references
                            DiaphragmId = diaphragmId
                        };

                        floors.Add(floor);
                    }
                }
                else
                {
                    // If no assignments found, create a floor with minimal data
                    var floor = new Floor
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR),
                        Points = new List<Point2D>(points)
                    };

                    floors.Add(floor);
                }
            }

            return floors;
        }
    }
}
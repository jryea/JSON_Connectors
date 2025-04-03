using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;

namespace ETABS.Export.Elements
{
    /// <summary>
    /// Exports point coordinates for the E2K file format with additional debugging
    /// </summary>
    public class PointCoordinatesExport
    {
        // Dictionary to store point IDs for reference by other exporters
        private Dictionary<Point2D, string> _pointMapping = new Dictionary<Point2D, string>();
        private StringBuilder _debugLog = new StringBuilder();

        /// <summary>
        /// Gets the point mapping dictionary for use by other exporters
        /// </summary>
        public Dictionary<Point2D, string> PointMapping => _pointMapping;

        /// <summary>
        /// Converts a collection of structural elements to E2K format text for point coordinates
        /// </summary>
        /// <param name="elements">Collection of structural elements from the model</param>
        /// <param name="layout">Layout information including grids and levels</param>
        /// <returns>E2K format text for point coordinates</returns>
        public string ConvertToE2K(ElementContainer elements, ModelLayoutContainer layout)
        {
            StringBuilder sb = new StringBuilder();

            // Clear debug log
            _debugLog.Clear();
            _debugLog.AppendLine("$ DEBUG POINT COORDINATES LOG");
            _debugLog.AppendLine("$ ===========================");

            // E2K Point Coordinates Header
            sb.AppendLine("$ POINT COORDINATES");

            // Clear any existing point mapping
            _pointMapping.Clear();

            // Dictionaries for tracking unique points
            Dictionary<string, Point2D> uniquePoints = new Dictionary<string, Point2D>();
            Dictionary<string, string> coordinateToId = new Dictionary<string, string>();
            int pointCounter = 1;

            // Log beam points for debugging
            _debugLog.AppendLine("$ BEAM POINT COLLECTION");
            if (elements.Beams != null && elements.Beams.Count > 0)
            {
                for (int i = 0; i < elements.Beams.Count; i++)
                {
                    var beam = elements.Beams[i];
                    _debugLog.AppendLine($"$ Beam {i + 1} (ID: {beam.Id}):");

                    if (beam.StartPoint != null)
                    {
                        _debugLog.AppendLine($"$   StartPoint: X={beam.StartPoint.X}, Y={beam.StartPoint.Y}, LevelId={beam.LevelId}");
                        AddUniquePoint(uniquePoints, coordinateToId, beam.StartPoint, ref pointCounter, $"Beam {i + 1} StartPoint");
                    }

                    if (beam.EndPoint != null)
                    {
                        _debugLog.AppendLine($"$   EndPoint: X={beam.EndPoint.X}, Y={beam.EndPoint.Y}, LevelId={beam.LevelId}");
                        AddUniquePoint(uniquePoints, coordinateToId, beam.EndPoint, ref pointCounter, $"Beam {i + 1} EndPoint");
                    }
                }
            }

            // Process Walls
            if (elements.Walls != null && elements.Walls.Count > 0)
            {
                foreach (var wall in elements.Walls)
                {
                    if (wall.Points != null)
                    {
                        foreach (var point in wall.Points)
                        {
                            AddUniquePoint(uniquePoints, coordinateToId, point, ref pointCounter, "Wall Point");
                        }
                    }
                }
            }

            // Process Floors
            if (elements.Floors != null && elements.Floors.Count > 0)
            {
                foreach (var floor in elements.Floors)
                {
                    if (floor.Points != null)
                    {
                        foreach (var point in floor.Points)
                        {
                            AddUniquePoint(uniquePoints, coordinateToId, point, ref pointCounter, "Floor Point");
                        }
                    }
                }
            }

            // Process Columns
            if (elements.Columns != null && elements.Columns.Count > 0)
            {
                foreach (var column in elements.Columns)
                {
                    if (column.StartPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, column.StartPoint, ref pointCounter, "Column StartPoint");

                    if (column.EndPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, column.EndPoint, ref pointCounter, "Column EndPoint");
                }
            }

            // Process Braces
            if (elements.Braces != null && elements.Braces.Count > 0)
            {
                foreach (var brace in elements.Braces)
                {
                    if (brace.StartPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, brace.StartPoint, ref pointCounter, "Brace StartPoint");

                    if (brace.EndPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, brace.EndPoint, ref pointCounter, "Brace EndPoint");
                }
            }

            // Process Grids (if needed)
            if (layout != null && layout.Grids != null && layout.Grids.Count > 0)
            {
                foreach (var grid in layout.Grids)
                {
                    if (grid.StartPoint != null)
                    {
                        var point = new Point2D(grid.StartPoint.X, grid.StartPoint.Y);
                        AddUniquePoint(uniquePoints, coordinateToId, point, ref pointCounter, "Grid StartPoint");
                    }

                    if (grid.EndPoint != null)
                    {
                        var point = new Point2D(grid.EndPoint.X, grid.EndPoint.Y);
                        AddUniquePoint(uniquePoints, coordinateToId, point, ref pointCounter, "Grid EndPoint");
                    }
                }
            }

            // Debug output of unique point keys and their assigned IDs
            _debugLog.AppendLine("$ UNIQUE POINTS COLLECTION");
            foreach (var entry in uniquePoints)
            {
                _debugLog.AppendLine($"$ Key: {entry.Key}, Point: X={entry.Value.X}, Y={entry.Value.Y}, ID: {coordinateToId[entry.Key]}");
            }

            // Format and write all unique points
            foreach (var entry in uniquePoints)
            {
                string pointId = coordinateToId[entry.Key];
                string pointLine = FormatPointLine(pointId, entry.Value);
                sb.AppendLine(pointLine);

                // Add to the point mapping for other exporters to use
                _pointMapping[entry.Value] = pointId;
            }

            // Debug output of final point mapping
            _debugLog.AppendLine("$ FINAL POINT MAPPING");
            foreach (var entry in _pointMapping)
            {
                _debugLog.AppendLine($"$ Point: X={entry.Key.X}, Y={entry.Key.Y}, ID: {entry.Value}");
            }

            // Add debug log to output
            sb.AppendLine();
            sb.Append(_debugLog.ToString());

            return sb.ToString();
        }

        /// <summary>
        /// Adds a point to the unique points dictionary if it doesn't already exist
        /// </summary>
        /// <param name="uniquePoints">Dictionary of unique points</param>
        /// <param name="coordinateToId">Dictionary mapping coordinate keys to point IDs</param>
        /// <param name="point">Point to add</param>
        /// <param name="counter">Reference to the point counter for naming</param>
        /// <param name="source">Source description for debugging</param>
        private void AddUniquePoint(Dictionary<string, Point2D> uniquePoints, Dictionary<string, string> coordinateToId,
                                   Point2D point, ref int counter, string source)
        {
            // For debugging, we'll use as exact precision as possible to identify issues
            // Create a key based on coordinates with fixed precision
            string key = $"{point.X.ToString("F10")},{point.Y.ToString("F10")}";
            _debugLog.AppendLine($"$   Adding point from {source}: X={point.X}, Y={point.Y}, Key={key}");

            if (!uniquePoints.ContainsKey(key))
            {
                _debugLog.AppendLine($"$     New unique point added with ID {counter}");
                uniquePoints.Add(key, point);
                string pointId = counter.ToString();
                coordinateToId[key] = pointId;
                counter++;
            }
            else
            {
                _debugLog.AppendLine($"$     Duplicate point, using existing ID {coordinateToId[key]}");
            }
        }

        /// <summary>
        /// Formats a point for the E2K file
        /// </summary>
        /// <param name="name">Name or ID of the point</param>
        /// <param name="point">Point coordinates</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatPointLine(string name, Point2D point)
        {
            // Format: POINT  "1"  -6843.36  -821.33
            return $"  POINT  \"{name}\"  {point.X.ToString("F10")}  {point.Y.ToString("F10")}";
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;

namespace ETABS.Export.Elements
{
    /// <summary>
    /// Exports point coordinates for the E2K file format
    /// </summary>
    public class PointCoordinatesToETABS
    {
        // Dictionary to store point IDs for reference by other exporters
        private Dictionary<Point2D, string> _pointMapping = new Dictionary<Point2D, string>(new Point2DComparer());

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

            // E2K Point Coordinates Header
            sb.AppendLine("$ POINT COORDINATES");

            // Clear any existing point mapping
            _pointMapping.Clear();

            // Dictionary to store unique points by coordinate key
            Dictionary<string, Point2D> uniquePoints = new Dictionary<string, Point2D>();
            Dictionary<string, string> coordinateToId = new Dictionary<string, string>();
            int pointCounter = 1;

            // Process Walls
            if (elements.Walls != null && elements.Walls.Count > 0)
            {
                foreach (var wall in elements.Walls)
                {
                    if (wall.Points != null)
                    {
                        foreach (var point in wall.Points)
                        {
                            AddUniquePoint(uniquePoints, coordinateToId, point, ref pointCounter);
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
                            AddUniquePoint(uniquePoints, coordinateToId, point, ref pointCounter);
                        }
                    }
                }
            }

            // Process Beams
            if (elements.Beams != null && elements.Beams.Count > 0)
            {
                foreach (var beam in elements.Beams)
                {
                    if (beam.StartPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, beam.StartPoint, ref pointCounter);

                    if (beam.EndPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, beam.EndPoint, ref pointCounter);
                }
            }

            // Process Columns
            if (elements.Columns != null && elements.Columns.Count > 0)
            {
                foreach (var column in elements.Columns)
                {
                    if (column.StartPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, column.StartPoint, ref pointCounter);

                    if (column.EndPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, column.EndPoint, ref pointCounter);
                }
            }

            // Process Braces
            if (elements.Braces != null && elements.Braces.Count > 0)
            {
                foreach (var brace in elements.Braces)
                {
                    if (brace.StartPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, brace.StartPoint, ref pointCounter);

                    if (brace.EndPoint != null)
                        AddUniquePoint(uniquePoints, coordinateToId, brace.EndPoint, ref pointCounter);
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
                        AddUniquePoint(uniquePoints, coordinateToId, point, ref pointCounter);
                    }

                    if (grid.EndPoint != null)
                    {
                        var point = new Point2D(grid.EndPoint.X, grid.EndPoint.Y);
                        AddUniquePoint(uniquePoints, coordinateToId, point, ref pointCounter);
                    }
                }
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

            return sb.ToString();
        }

        /// <summary>
        /// Adds a point to the unique points dictionary if it doesn't already exist
        /// </summary>
        /// <param name="uniquePoints">Dictionary of unique points</param>
        /// <param name="coordinateToId">Dictionary mapping coordinate keys to point IDs</param>
        /// <param name="point">Point to add</param>
        /// <param name="counter">Reference to the point counter for naming</param>
        private void AddUniquePoint(Dictionary<string, Point2D> uniquePoints, Dictionary<string, string> coordinateToId,
                                   Point2D point, ref int counter)
        {
            // Create a unique key based on coordinates with fixed precision (6 decimal places)
            string key = $"{Math.Round(point.X, 6)},{Math.Round(point.Y, 6)}";

            if (!uniquePoints.ContainsKey(key))
            {
                uniquePoints.Add(key, point);
                string pointId = counter.ToString();
                coordinateToId[key] = pointId;
                counter++;
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
            return $"  POINT  \"{name}\"  {point.X:F2}  {point.Y:F2}";
        }
    }

    /// <summary>
    /// Custom comparer for Point2D objects to ensure proper equality checking
    /// </summary>
    public class Point2DComparer : IEqualityComparer<Point2D>
    {
        private const double Tolerance = 1e-6;

        public bool Equals(Point2D x, Point2D y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;

            return Math.Abs(x.X - y.X) < Tolerance && Math.Abs(x.Y - y.Y) < Tolerance;
        }

        public int GetHashCode(Point2D obj)
        {
            if (obj == null)
                return 0;

            // Round to fixed precision for consistent hash codes
            double roundedX = Math.Round(obj.X, 6);
            double roundedY = Math.Round(obj.Y, 6);

            unchecked
            {
                int hash = 17;
                hash = hash * 23 + roundedX.GetHashCode();
                hash = hash * 23 + roundedY.GetHashCode();
                return hash;
            }
        }
    }
}
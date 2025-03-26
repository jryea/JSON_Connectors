using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;

namespace ETABS.Export.Model
{
    /// <summary>
    /// Exports area connectivities for the E2K file format
    /// </summary>
    public class AreaConnectivitiesExport
    {
        private readonly Dictionary<Point2D, string> _pointMapping;

        /// <summary>
        /// Constructor that takes a point mapping dictionary
        /// </summary>
        /// <param name="pointMapping">Dictionary mapping points to their IDs</param>
        public AreaConnectivitiesExport(Dictionary<Point2D, string> pointMapping)
        {
            _pointMapping = pointMapping ?? new Dictionary<Point2D, string>();
        }

        /// <summary>
        /// Converts structural elements to E2K format text for area connectivities
        /// </summary>
        /// <param name="elements">Collection of structural elements from the model</param>
        /// <returns>E2K format text for area connectivities</returns>
        public string ConvertToE2K(ElementContainer elements)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Area Connectivities Header
            sb.AppendLine("$ AREA CONNECTIVITIES");

            // Process Walls
            int wallCounter = 1;
            if (elements.Walls != null && elements.Walls.Count > 0)
            {
                foreach (var wall in elements.Walls)
                {
                    if (wall.Points != null && wall.Points.Count >= 3)
                    {
                        string wallLine = FormatWallLine(wall, wallCounter);
                        sb.AppendLine(wallLine);
                        wallCounter++;
                    }
                }
            }

            // Process Floors
            int floorCounter = 1;
            if (elements.Floors != null && elements.Floors.Count > 0)
            {
                foreach (var floor in elements.Floors)
                {
                    if (floor.Points != null && floor.Points.Count >= 3)
                    {
                        string floorLine = FormatFloorLine(floor, floorCounter);
                        sb.AppendLine(floorLine);
                        floorCounter++;
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a wall for the E2K file
        /// </summary>
        /// <param name="wall">Wall element</param>
        /// <param name="counter">Wall counter for naming</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatWallLine(Wall wall, int counter)
        {
            StringBuilder sb = new StringBuilder();

            // Get the number of vertices (without duplicating the first point in the count)
            int numVertices = wall.Points.Count;

            sb.Append($"  AREA \"W{counter}\" PANEL {numVertices}");

            // Add point references
            foreach (var point in wall.Points)
            {
                string pointId = GetPointId(point);
                sb.Append($" \"{pointId}\"");
            }

            // Repeat the first point to close the polygon if not already closed
            if (wall.Points.Count > 0 && !PointsEqual(wall.Points[0], wall.Points[wall.Points.Count - 1]))
            {
                string firstPointId = GetPointId(wall.Points[0]);
                sb.Append($" \"{firstPointId}\"");
            }

            // Add additional parameters (fixed values for now)
            sb.Append(" 1 1 0 0");

            return sb.ToString();
        }

        /// <summary>
        /// Formats a floor for the E2K file
        /// </summary>
        /// <param name="floor">Floor element</param>
        /// <param name="counter">Floor counter for naming</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatFloorLine(Floor floor, int counter)
        {
            StringBuilder sb = new StringBuilder();

            // Get the number of vertices (without duplicating the first point in the count)
            int numVertices = floor.Points.Count;

            sb.Append($"  AREA \"F{counter}\"  FLOOR {numVertices}");

            // Add point references
            foreach (var point in floor.Points)
            {
                string pointId = GetPointId(point);
                sb.Append($"  \"{pointId}\"");
            }

            // Add zeros for additional parameters (one for each vertex)
            for (int i = 0; i < numVertices; i++)
            {
                sb.Append(" 0");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the ID for a point using the point mapping dictionary
        /// </summary>
        /// <param name="point">Point to get ID for</param>
        /// <returns>Point ID string</returns>
        private string GetPointId(Point2D point)
        {
            // Check for exact match
            foreach (var entry in _pointMapping)
            {
                if (Math.Abs(entry.Key.X - point.X) < 0.001 && Math.Abs(entry.Key.Y - point.Y) < 0.001)
                {
                    return entry.Value;
                }
            }

            // If no exact match found, try to find the closest point
            double minDistance = double.MaxValue;
            string closestPointId = "0";

            foreach (var entry in _pointMapping)
            {
                double distance = Math.Sqrt(
                    Math.Pow(entry.Key.X - point.X, 2) +
                    Math.Pow(entry.Key.Y - point.Y, 2));

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPointId = entry.Value;
                }
            }

            // Only use closest point if it's within a reasonable tolerance
            if (minDistance < 0.1)
            {
                return closestPointId;
            }

            // Default to "0" if no match found
            return "0";
        }

        /// <summary>
        /// Checks if two points are equal within a small tolerance
        /// </summary>
        /// <param name="p1">First point</param>
        /// <param name="p2">Second point</param>
        /// <returns>True if points are equal within tolerance</returns>
        private bool PointsEqual(Point2D p1, Point2D p2)
        {
            const double tolerance = 0.001;
            return Math.Abs(p1.X - p2.X) < tolerance && Math.Abs(p1.Y - p2.Y) < tolerance;
        }
    }
}
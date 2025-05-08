// Wall and Floor Connectivity Classes
using Core.Models.Elements;
using Core.Models.Geometry;
using System.Collections.Generic;
using System.Text;
using System;
using System.Diagnostics;

namespace ETABS.Import.Elements.AreaConnectivity
{
    // Converts wall connectivity information to ETABS E2K format
    public class WallConnectivityImport : IConnectivityImport
    {
        private readonly PointCoordinatesImport _pointCoordinates;
        private readonly Dictionary<string, string> _wallIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectivityByCoordinates = new Dictionary<string, string>();
        private List<Wall> _walls;

        // Constructor
        public WallConnectivityImport(PointCoordinatesImport pointCoordinates)
        {
            _pointCoordinates = pointCoordinates ?? throw new ArgumentNullException(nameof(pointCoordinates));
        }

        // Sets the walls to convert
        public void SetWalls(List<Wall> walls)
        {
            _walls = walls;
        }

        // Converts wall connectivities to E2K format
        public string ExportConnectivities()
        {
            StringBuilder sb = new StringBuilder();
            int wallCounter = 1;

            if (_walls == null || _walls.Count == 0)
                return sb.ToString();

            foreach (var wall in _walls)
            {
                if (wall.Points == null || wall.Points.Count < 2)
                    continue;

                // Generate a unique key for the wall's points
                string coordinateKey = GenerateWallCoordinateKey(wall.Points);

                if (_connectivityByCoordinates.TryGetValue(coordinateKey, out string existingWallId))
                {
                    // Reuse existing connectivity
                    _wallIdMapping[wall.Id] = existingWallId;
                    continue;
                }

                // Create a new wall ID with the prefix "W"
                string wallId = $"W{wallCounter++}";
                _wallIdMapping[wall.Id] = wallId;

                // Get point IDs for all wall vertices
                List<string> pointIds = new List<string>();
                foreach (var point in wall.Points)
                {
                    string pointId = _pointCoordinates.GetOrCreatePointId(point);
                    pointIds.Add(pointId);
                }

                // Format wall connectivity
                sb.AppendLine(FormatWallConnectivity(wallId, pointIds));

                // Store this connectivity
                _connectivityByCoordinates[coordinateKey] = wallId;
            }
            return sb.ToString();
        }

        // Gets the mapping from source wall IDs to E2K wall IDs
        public Dictionary<string, string> GetIdMapping()
        {
            return _wallIdMapping;
        }

        // Generates a unique key for a wall based on its points
        private string GenerateWallCoordinateKey(List<Point2D> points)
        {
            // For linear walls (2 points), ensure consistent orientation
            if (points.Count == 2)
            {
                var p1 = points[0];
                var p2 = points[1];

                // Determine ordering based on X,Y coordinates
                if ((Math.Abs(p2.X - p1.X) > Math.Abs(p2.Y - p1.Y) && p2.X < p1.X) ||
                    (Math.Abs(p2.Y - p1.Y) >= Math.Abs(p2.X - p1.X) && p2.Y < p1.Y))
                {
                    // Swap orientation for consistent key
                    return $"{Math.Round(p2.X, 6)},{Math.Round(p2.Y, 6)}_{Math.Round(p1.X, 6)},{Math.Round(p1.Y, 6)}";
                }

                return $"{Math.Round(p1.X, 6)},{Math.Round(p1.Y, 6)}_{Math.Round(p2.X, 6)},{Math.Round(p2.Y, 6)}";
            }

            // For multi-point walls, concatenate all points in a consistent order
            var pointStrings = new List<string>();
            foreach (var point in points)
            {
                pointStrings.Add($"{Math.Round(point.X, 6)},{Math.Round(point.Y, 6)}");
            }

            // Sort points for consistent ordering regardless of input order
            pointStrings.Sort();
            return string.Join("_", pointStrings);
        }

        // Formats a wall connectivity line for E2K format
        private string FormatWallConnectivity(string areaId, List<string> pointIds)
        {
            if (pointIds.Count != 2)
            {
                return "";
            }

            string pt1 = pointIds[0];
            string pt2 = pointIds[1];

            // Format: AREA "W1" WALL 4 "1" "2" "2" "1" 1 1 0 0
            // Describes a vertical wall with 4 points starting at the top story
            return $"  AREA \"{areaId}\" PANEL 4 \"{pt1}\" \"{pt2}\" \"{pt2}\" \"{pt1}\" 1 1 0 0";

        }
    }
}

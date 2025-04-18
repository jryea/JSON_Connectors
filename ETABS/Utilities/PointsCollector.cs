using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models.Geometry;

namespace ETABS.Utilities
{

    // Utility class to parse and collect point coordinates from E2K files
    public class PointsCollector
    {
        // Dictionary to store point IDs mapped to their coordinates
        private Dictionary<string, Point3D> _points = new Dictionary<string, Point3D>();

        // Gets the collection of points that have been parsed
        public Dictionary<string, Point3D> Points => _points;

        // Parses the POINT COORDINATES section from E2K content and populates the points dictionary
  
        public void ParsePoints(string pointCoordinatesSection)
        {
            if (string.IsNullOrWhiteSpace(pointCoordinatesSection))
                return;

            // Regular expression to match point coordinate lines
            // Format: POINT "1" -252 756
            var pointPattern = new Regex(@"^\s*POINT\s+""([^""]+)""\s+([\d\.\-]+)\s+([\d\.\-]+)(?:\s+([\d\.\-]+))?",
                RegexOptions.Multiline);

            var matches = pointPattern.Matches(pointCoordinatesSection);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    string pointId = match.Groups[1].Value;
                    double x = Convert.ToDouble(match.Groups[2].Value);
                    double y = Convert.ToDouble(match.Groups[3].Value);

                    // Z coordinate is optional in E2K
                    double z = 0;
                    if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value))
                    {
                        z = Convert.ToDouble(match.Groups[4].Value);
                    }

                    _points[pointId] = new Point3D(x, y, z);
                }
            }
        }

        // Gets a Point2D representation of a point by its ID
        public Point2D GetPoint2D(string pointId)
        {
            if (_points.TryGetValue(pointId, out Point3D point3D))
            {
                return new Point2D(point3D.X, point3D.Y);
            }
            return null;
        }

        // Gets a Point3D representation of a point by its ID
        public Point3D GetPoint3D(string pointId)
        {
            if (_points.TryGetValue(pointId, out Point3D point3D))
            {
                return point3D;
            }
            return null;
        }
        
        // Creates a GridPoint from a point ID
        public GridPoint GetGridPoint(string pointId, bool isBubble = true)
        {
            if (_points.TryGetValue(pointId, out Point3D point3D))
            {
                return new GridPoint(point3D.X, point3D.Y, point3D.Z, isBubble);
            }
            return null;
        }
    }
}
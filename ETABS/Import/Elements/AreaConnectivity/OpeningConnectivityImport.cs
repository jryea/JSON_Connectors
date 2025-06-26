using Core.Models.Elements;
using Core.Models.Geometry;
using System.Collections.Generic;
using System.Text;
using System;

namespace ETABS.Import.Elements.AreaConnectivity
{
    // Converts opening connectivity information to ETABS E2K format
    public class OpeningConnectivityImport : IConnectivityImport
    {
        private readonly PointCoordinatesImport _pointCoordinates;
        private readonly Dictionary<string, string> _openingIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectivityByCoordinates = new Dictionary<string, string>();

        private List<Opening> _openings;

        // Constructor
        public OpeningConnectivityImport(PointCoordinatesImport pointCoordinates)
        {
            _pointCoordinates = pointCoordinates ?? throw new ArgumentNullException(nameof(pointCoordinates));
        }

        // Sets the openings to convert
        public void SetOpenings(List<Opening> openings)
        {
            _openings = openings;
        }

        // Converts opening connectivities to E2K format
        public string ExportConnectivities()
        {
            StringBuilder sb = new StringBuilder();
            int openingCounter = 1;

            if (_openings == null || _openings.Count == 0)
                return sb.ToString();

            foreach (var opening in _openings)
            {
                if (opening.Points == null || opening.Points.Count < 3)
                    continue;

                // Generate a unique key for the opening's points
                string coordinateKey = GenerateOpeningCoordinateKey(opening.Points);

                if (_connectivityByCoordinates.TryGetValue(coordinateKey, out string existingOpeningId))
                {
                    // Reuse existing connectivity
                    _openingIdMapping[opening.Id] = existingOpeningId;
                    continue;
                }

                // Create a new opening ID with the prefix "A"
                string openingId = $"A{openingCounter++}";
                _openingIdMapping[opening.Id] = openingId;

                // Get point IDs for all opening vertices
                List<string> pointIds = new List<string>();
                foreach (var point in opening.Points)
                {
                    string pointId = _pointCoordinates.GetOrCreatePointId(point);
                    pointIds.Add(pointId);
                }

                // Format opening connectivity
                sb.AppendLine(FormatOpeningConnectivity(openingId, pointIds));

                // Store this connectivity
                _connectivityByCoordinates[coordinateKey] = openingId;
            }

            return sb.ToString();
        }

        // Gets the mapping from source opening IDs to E2K opening IDs
        public Dictionary<string, string> GetIdMapping()
        {
            return _openingIdMapping;
        }

        // Generates a unique key for an opening based on its points
        private string GenerateOpeningCoordinateKey(List<Point2D> points)
        {
            var pointStrings = new List<string>();
            foreach (var point in points)
            {
                pointStrings.Add($"{Math.Round(point.X, 6)},{Math.Round(point.Y, 6)}");
            }

            // Sort points for consistent ordering regardless of input order
            pointStrings.Sort();

            return string.Join("_", pointStrings);
        }

        // Formats an opening connectivity line for E2K format
        private string FormatOpeningConnectivity(string areaId, List<string> pointIds)
        {
            // Format: AREA "A1" AREA 4 "6" "10" "11" "7" 0 0 0 0
            return $"  AREA \"{areaId}\" AREA {pointIds.Count} {string.Join(" ", pointIds.ConvertAll(id => $"\"{id}\""))} 0 0 0 0";
        }
    }
}
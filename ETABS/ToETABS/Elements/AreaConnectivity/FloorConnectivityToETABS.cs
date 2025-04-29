using Core.Models.Elements;
using Core.Models.Geometry;
using System.Collections.Generic;
using System.Text;

using System;

namespace ETABS.ToETABS.Elements.AreaConnectivity
{
    // Converts floor connectivity information to ETABS E2K format
        public class FloorConnectivityToETABS : IConnectivityToETABS
        {
        private readonly PointCoordinatesToETABS _pointCoordinates;
        private readonly Dictionary<string, string> _floorIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectivityByCoordinates = new Dictionary<string, string>();

        private List<Floor> _floors;

        // Constructor
        public FloorConnectivityToETABS(PointCoordinatesToETABS pointCoordinates)
        {
            _pointCoordinates = pointCoordinates ?? throw new ArgumentNullException(nameof(pointCoordinates));
        }

        // Sets the floors to convert
        public void SetFloors(List<Floor> floors)
        {
            _floors = floors;
        }

        // Converts floor connectivities to E2K format
        public string ExportConnectivities()
        {
            StringBuilder sb = new StringBuilder();
            int floorCounter = 1;

            if (_floors == null || _floors.Count == 0)
                return sb.ToString();

            foreach (var floor in _floors)
            {
                if (floor.Points == null || floor.Points.Count < 3)
                    continue;

                // Generate a unique key for the floor's points
                string coordinateKey = GenerateFloorCoordinateKey(floor.Points);

                if (_connectivityByCoordinates.TryGetValue(coordinateKey, out string existingFloorId))
                {
                    // Reuse existing connectivity
                    _floorIdMapping[floor.Id] = existingFloorId;
                    continue;
                }

                // Create a new floor ID with the prefix "F"
                string floorId = $"F{floorCounter++}";
                _floorIdMapping[floor.Id] = floorId;

                // Get point IDs for all floor vertices
                List<string> pointIds = new List<string>();
                foreach (var point in floor.Points)
                {
                    string pointId = _pointCoordinates.GetOrCreatePointId(point);
                    pointIds.Add(pointId);
                }

                // Format floor connectivity
                sb.AppendLine(FormatFloorConnectivity(floorId, pointIds));

                // Store this connectivity
                _connectivityByCoordinates[coordinateKey] = floorId;
            }

            return sb.ToString();
        }

        // Gets the mapping from source floor IDs to E2K floor IDs
        public Dictionary<string, string> GetIdMapping()
        {
            return _floorIdMapping;
        }

        // Generates a unique key for a floor based on its points
        private string GenerateFloorCoordinateKey(List<Point2D> points)
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

        // Formats a floor connectivity line for E2K format
        private string FormatFloorConnectivity(string areaId, List<string> pointIds)
        {
            // Format: AREA "F1" FLOOR 4 "1" "2" "3" "4" 0 0 0 0
            return $"  AREA \"{areaId}\" FLOOR {pointIds.Count} {string.Join(" ", pointIds.ConvertAll(id => $"\"{id}\""))} 0 0 0 0";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Utilities;

namespace ETABS.Import.Elements.LineConnectivity
{
    public class ColumnConnectivityImport : IConnectivityImport
    {
        private readonly PointCoordinatesImport _pointCoordinates;
        private readonly Dictionary<string, string> _columnIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectivityByCoordinates = new Dictionary<string, string>();

        private List<Column> _columns;

        public ColumnConnectivityImport(PointCoordinatesImport pointCoordinates)
        {
            _pointCoordinates = pointCoordinates ?? throw new ArgumentNullException(nameof(pointCoordinates));
        }

        public void SetColumns(List<Column> columns)
        {
            _columns = columns;
        }

        public string ExportConnectivities()
        {
            StringBuilder sb = new StringBuilder();
            int columnCounter = 1;

            if (_columns == null || _columns.Count == 0)
                return sb.ToString();

            // Key is normalized coordinate string, value is the LINE ID
            var uniqueColumnLocations = new Dictionary<string, string>();

            foreach (var column in _columns)
            {
                if (column.StartPoint == null)
                    continue;

                // Normalize the coordinates
                double x = Math.Round(column.StartPoint.X * 4) / 4;
                double y = Math.Round(column.StartPoint.Y * 4) / 4;

                // Create a location key for this column's position
                string locationKey = $"{x:F2},{y:F2}";

                // Check if we already have a column at this location
                if (uniqueColumnLocations.TryGetValue(locationKey, out string existingColumnId))
                {
                    // Reuse existing connectivity
                    _columnIdMapping[column.Id] = existingColumnId;
                    continue;
                }

                // New location - create a column connectivity
                string columnId = $"C{columnCounter++}";
                _columnIdMapping[column.Id] = columnId;
                uniqueColumnLocations[locationKey] = columnId;

                // Get point ID from the coordinates manager
                string pointId = _pointCoordinates.GetOrCreatePointId(column.StartPoint);

                // Create vertical LINE with same point ID for start and end
                sb.AppendLine($"  LINE \"{columnId}\" COLUMN \"{pointId}\" \"{pointId}\" 1");
            }

            return sb.ToString();
        }

        public Dictionary<string, string> GetIdMapping()
        {
            return _columnIdMapping;
        }
    }
}
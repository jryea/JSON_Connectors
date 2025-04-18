using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Utilities;

namespace ETABS.ToETABS.Elements.Connectivity
{
    /// <summary>
    /// Converts column connectivity information to ETABS E2K format
    /// </summary>
    public class ColumnConnectivityToETABS : IConnectivityToETABS
    {
        private readonly PointCoordinatesToETABS _pointCoordinates;
        private readonly Dictionary<string, string> _columnIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectivityByCoordinates = new Dictionary<string, string>();

        private List<Column> _columns;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pointCoordinates">The point coordinates manager instance</param>
        public ColumnConnectivityToETABS(PointCoordinatesToETABS pointCoordinates)
        {
            _pointCoordinates = pointCoordinates ?? throw new ArgumentNullException(nameof(pointCoordinates));
        }

        /// <summary>
        /// Sets the columns to convert
        /// </summary>
        /// <param name="columns">Collection of columns</param>
        public void SetColumns(List<Column> columns)
        {
            _columns = columns;
        }

        /// <summary>
        /// Converts column connectivities to E2K format
        /// </summary>
        /// <returns>E2K formatted column connectivities</returns>
        public string ExportConnectivities()
        {
            StringBuilder sb = new StringBuilder();
            int columnCounter = 1;

            if (_columns == null || _columns.Count == 0)
                return sb.ToString();

            // Group columns by their start point coordinates
            var columnGroups = new Dictionary<string, List<Column>>();

            foreach (var column in _columns)
            {
                if (column.StartPoint == null)
                    continue;

                string coordinateKey = Point2DComparer.GetCoordinateKey(column.StartPoint);

                if (!columnGroups.ContainsKey(coordinateKey))
                    columnGroups[coordinateKey] = new List<Column>();

                columnGroups[coordinateKey].Add(column);
            }

            foreach (var group in columnGroups)
            {
                // Take the first column from the group
                var column = group.Value[0];

                // Create a column ID
                string columnId = $"C{columnCounter++}";

                // Get the point ID from the central point coordinator
                string pointId = _pointCoordinates.GetOrCreatePointId(column.StartPoint);

                // Create line connectivity with the same point ID for both ends
                // Format: LINE "C1" COLUMN "9" "9" 1
                sb.AppendLine($"  LINE \"{columnId}\" COLUMN \"{pointId}\" \"{pointId}\" 1");

                // Store the mapping from all columns in this group to the ID
                foreach (var col in group.Value)
                {
                    _columnIdMapping[col.Id] = columnId;
                }

                // Store this connectivity
                _connectivityByCoordinates[group.Key] = columnId;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the mapping from source column IDs to E2K column IDs
        /// </summary>
        /// <returns>Dictionary mapping source IDs to E2K IDs</returns>
        public Dictionary<string, string> GetIdMapping()
        {
            return _columnIdMapping;
        }
    }
}
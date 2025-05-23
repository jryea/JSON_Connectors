﻿using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Utilities;

namespace ETABS.Import.Elements.LineConnectivity
{
    /// <summary>
    /// Converts brace connectivity information to ETABS E2K format
    /// </summary>
    public class BraceConnectivityImport : IConnectivityImport
    {
        private readonly PointCoordinatesImport _pointCoordinates;
        private readonly Dictionary<string, string> _braceIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectivityByCoordinates = new Dictionary<string, string>();

        private List<Brace> _braces;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pointCoordinates">The point coordinates manager instance</param>
        public BraceConnectivityImport(PointCoordinatesImport pointCoordinates)
        {
            _pointCoordinates = pointCoordinates ?? throw new ArgumentNullException(nameof(pointCoordinates));
        }

        /// <summary>
        /// Sets the braces to convert
        /// </summary>
        /// <param name="braces">Collection of braces</param>
        public void SetBraces(List<Brace> braces)
        {
            _braces = braces;
        }

        /// <summary>
        /// Converts brace connectivities to E2K format
        /// </summary>
        /// <returns>E2K formatted brace connectivities</returns>
        public string ExportConnectivities()
        {
            StringBuilder sb = new StringBuilder();
            int braceCounter = 1;

            if (_braces == null || _braces.Count == 0)
                return sb.ToString();

            foreach (var brace in _braces)
            {
                if (brace.StartPoint == null || brace.EndPoint == null)
                    continue;

                // Generate a unique key for the brace's start and end coordinates
                string coordinateKey = Point2DComparer.GetLineCoordinateKey(brace.StartPoint, brace.EndPoint);

                if (_connectivityByCoordinates.TryGetValue(coordinateKey, out string existingBraceId))
                {
                    // Reuse existing connectivity
                    _braceIdMapping[brace.Id] = existingBraceId;
                    continue;
                }

                // Create a new brace ID with the prefix "D"
                string braceId = $"D{braceCounter++}";
                _braceIdMapping[brace.Id] = braceId;

                // Get point IDs from the central point coordinator
                string startPointId = _pointCoordinates.GetOrCreatePointId(brace.StartPoint);
                string endPointId = _pointCoordinates.GetOrCreatePointId(brace.EndPoint);

                // Format brace connectivity
                sb.AppendLine($"  LINE \"{braceId}\" BRACE \"{startPointId}\" \"{endPointId}\" 1");

                // Store this connectivity
                _connectivityByCoordinates[coordinateKey] = braceId;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the mapping from source brace IDs to E2K brace IDs
        /// </summary>
        /// <returns>Dictionary mapping source IDs to E2K IDs</returns>
        public Dictionary<string, string> GetIdMapping()
        {
            return _braceIdMapping;
        }
    }
}

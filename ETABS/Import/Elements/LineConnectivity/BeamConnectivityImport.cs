using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Utilities;

namespace ETABS.Import.Elements.LineConnectivity
{
    /// <summary>
    /// Converts beam connectivity information to ETABS E2K format
    /// </summary>
    public class BeamConnectivityImport : IConnectivityImport
    {
        private readonly PointCoordinatesImport _pointCoordinates;
        private readonly Dictionary<string, string> _beamIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectivityByCoordinates = new Dictionary<string, string>();

        private List<Beam> _beams;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pointCoordinates">The point coordinates manager instance</param>
        public BeamConnectivityImport(PointCoordinatesImport pointCoordinates)
        {
            _pointCoordinates = pointCoordinates ?? throw new ArgumentNullException(nameof(pointCoordinates));
        }

        /// <summary>
        /// Sets the beams to convert
        /// </summary>
        /// <param name="beams">Collection of beams</param>
        public void SetBeams(List<Beam> beams)
        {
            _beams = beams;
        }

        /// <summary>
        /// Converts beam connectivities to E2K format
        /// </summary>
        /// <returns>E2K formatted beam connectivities</returns>
        public string ExportConnectivities()
        {
            StringBuilder sb = new StringBuilder();
            int beamCounter = 1;

            if (_beams == null || _beams.Count == 0)
                return sb.ToString();

            foreach (var beam in _beams)
            {
                if (beam.StartPoint == null || beam.EndPoint == null)
                    continue;

                // Check if we already have a connectivity for these coordinates
                string coordinateKey = Point2DComparer.GetLineCoordinateKey(beam.StartPoint, beam.EndPoint);

                if (_connectivityByCoordinates.TryGetValue(coordinateKey, out string existingBeamId))
                {
                    // Reuse existing connectivity
                    _beamIdMapping[beam.Id] = existingBeamId;
                    continue;
                }

                // Create a new beam ID
                string beamId = $"B{beamCounter++}";
                _beamIdMapping[beam.Id] = beamId;

                // Get point IDs using the point coordinates manager
                string startPointId = _pointCoordinates.GetOrCreatePointId(beam.StartPoint);
                string endPointId = _pointCoordinates.GetOrCreatePointId(beam.EndPoint);

                // Format beam connectivity
                // Format: LINE "B1" BEAM "1" "2" 0
                sb.AppendLine($"  LINE \"{beamId}\" BEAM \"{startPointId}\" \"{endPointId}\" 0");

                // Store this connectivity
                _connectivityByCoordinates[coordinateKey] = beamId;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the mapping from source beam IDs to E2K beam IDs
        /// </summary>
        /// <returns>Dictionary mapping source IDs to E2K IDs</returns>
        public Dictionary<string, string> GetIdMapping()
        {
            return _beamIdMapping;
        }
    }
}
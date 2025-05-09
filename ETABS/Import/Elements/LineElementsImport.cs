using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using ETABS.Import.Elements.LineConnectivity;
using ETABS.Import.Elements.LineAssignment;
using Core.Models.Geometry;

namespace ETABS.Import.Elements
{
    // Coordinator class for converting line elements (beams, columns, braces) to ETABS E2K format
    public class LineElementsImport
    {
        private readonly BeamConnectivityImport _beamConnectivityImport;
        private readonly ColumnConnectivityImport _columnConnectivityImport;
        private readonly BraceConnectivityImport _braceConnectivityImport;

        private readonly BeamAssignmentImport _beamAssignmentImport;
        private readonly ColumnAssignmentImport _columnAssignmentImport;
        private readonly BraceAssignmentImport _braceAssignmentImport;

        // Constructor that takes a PointCoordinatesToETABS instance
        public LineElementsImport(PointCoordinatesImport pointCoordinates)
        {
            if (pointCoordinates == null)
                throw new ArgumentNullException(nameof(pointCoordinates));

            // Initialize specialized converters with the point coordinates instance
            _beamConnectivityImport = new BeamConnectivityImport(pointCoordinates);
            _columnConnectivityImport = new ColumnConnectivityImport(pointCoordinates);
            _braceConnectivityImport = new BraceConnectivityImport(pointCoordinates);

            _beamAssignmentImport = new BeamAssignmentImport();
            _columnAssignmentImport = new ColumnAssignmentImport();
            _braceAssignmentImport = new BraceAssignmentImport();
        }

        // Converts line elements (beams, columns, braces) to ETABS E2K format
        public string ConvertToE2K(
            ElementContainer elements,
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties)
        {
            StringBuilder sb = new StringBuilder();

            // Set data for connectivity converters
            _beamConnectivityImport.SetBeams(elements.Beams);
            _columnConnectivityImport.SetColumns(elements.Columns);
            _braceConnectivityImport.SetBraces(elements.Braces);

            // Set data for assignment converters
            _beamAssignmentImport.SetData(elements.Beams, levels, frameProperties);
            _columnAssignmentImport.SetData(elements.Columns, levels, frameProperties);
            _braceAssignmentImport.SetData(elements.Braces, levels, frameProperties);

            // Process all line connectivities
            sb.AppendLine("$ LINE CONNECTIVITIES");

            // Process column connectivities first (since they're often referenced by beams/braces)
            string columnConnectivities = _columnConnectivityImport.ExportConnectivities();
            sb.AppendLine(columnConnectivities);

            // Process beam connectivities
            string beamConnectivities = _beamConnectivityImport.ExportConnectivities();
            sb.AppendLine(beamConnectivities);

            // Process brace connectivities
            string braceConnectivities = _braceConnectivityImport.ExportConnectivities();
            sb.AppendLine(braceConnectivities);

            sb.AppendLine();

            // Process all line assignments
            sb.AppendLine("$ LINE ASSIGNS");

            // Get ID mappings from connectivity converters
            var columnIdMapping = _columnConnectivityImport.GetIdMapping();
            var beamIdMapping = _beamConnectivityImport.GetIdMapping();
            var braceIdMapping = _braceConnectivityImport.GetIdMapping();

            // Process column assignments
            string columnAssignments = _columnAssignmentImport.ExportAssignments(columnIdMapping);
            sb.AppendLine(columnAssignments);

            // Process beam assignments
            string beamAssignments = _beamAssignmentImport.ExportAssignments(beamIdMapping);
            sb.AppendLine(beamAssignments);

            // Process brace assignments
            string braceAssignments = _braceAssignmentImport.ExportAssignments(braceIdMapping);
            sb.AppendLine(braceAssignments);

            return sb.ToString();
        }
    }
}
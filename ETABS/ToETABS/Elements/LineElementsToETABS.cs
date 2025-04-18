using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using ETABS.ToETABS.Elements.Connectivity;
using ETABS.ToETABS.Elements.Assignment;
using Core.Models.Geometry;

namespace ETABS.ToETABS.Elements
{
    // Coordinator class for converting line elements (beams, columns, braces) to ETABS E2K format
    public class LineElementsToETABS
    {
        private readonly BeamConnectivityToETABS _beamConnectivityToETABS;
        //private readonly ColumnConnectivityToETABS _columnConnectivityToETABS;
        //private readonly BraceConnectivityToETABS _braceConnectivityToETABS;

        private readonly BeamAssignmentToETABS _beamAssignmentToETABS;
        //private readonly ColumnAssignmentToETABS _columnAssignmentToETABS;
        //private readonly BraceAssignmentToETABS _braceAssignmentToETABS;

        // Constructor that takes a PointCoordinatesToETABS instance
        public LineElementsToETABS(PointCoordinatesToETABS pointCoordinates)
        {
            if (pointCoordinates == null)
                throw new ArgumentNullException(nameof(pointCoordinates));

            // Initialize specialized converters with the point coordinates instance
            _beamConnectivityToETABS = new BeamConnectivityToETABS(pointCoordinates);
            //_columnConnectivityToETABS = new ColumnConnectivityToETABS(pointCoordinates);
            //_braceConnectivityToETABS = new BraceConnectivityToETABS(pointCoordinates);

            _beamAssignmentToETABS = new BeamAssignmentToETABS();
            //_columnAssignmentToETABS = new ColumnAssignmentToETABS();
            //_braceAssignmentToETABS = new BraceAssignmentToETABS();
        }

        // Converts line elements (beams, columns, braces) to ETABS E2K format
        public string ConvertToE2K(
            ElementContainer elements,
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties)
        {
            StringBuilder sb = new StringBuilder();

            // Set data for connectivity converters
            _beamConnectivityToETABS.SetBeams(elements.Beams);
            //_columnConnectivityToETABS.SetColumns(elements.Columns);
            //_braceConnectivityToETABS.SetBraces(elements.Braces);

            // Set data for assignment converters
            _beamAssignmentToETABS.SetData(elements.Beams, levels, frameProperties);
            //_columnAssignmentToETABS.SetData(elements.Columns, levels, frameProperties);
            //_braceAssignmentToETABS.SetData(elements.Braces, levels, frameProperties);

            // Process all line connectivities
            sb.AppendLine("$ LINE CONNECTIVITIES");

            // Process column connectivities first (since they're often referenced by beams/braces)
            //string columnConnectivities = _columnConnectivityToETABS.ExportConnectivities();
            //sb.AppendLine(columnConnectivities);

            // Process beam connectivities
            string beamConnectivities = _beamConnectivityToETABS.ExportConnectivities();
            sb.AppendLine(beamConnectivities);

            // Process brace connectivities
            //string braceConnectivities = _braceConnectivityToETABS.ExportConnectivities();
            //sb.AppendLine(braceConnectivities);

            sb.AppendLine();

            // Process all line assignments
            sb.AppendLine("$ LINE ASSIGNS");

            // Get ID mappings from connectivity converters
            //var columnIdMapping = _columnConnectivityToETABS.GetIdMapping();
            var beamIdMapping = _beamConnectivityToETABS.GetIdMapping();
            //var braceIdMapping = _braceConnectivityToETABS.GetIdMapping();

            // Process column assignments
            //string columnAssignments = _columnAssignmentToETABS.ExportAssignments(columnIdMapping);
            //sb.AppendLine(columnAssignments);

            // Process beam assignments
            string beamAssignments = _beamAssignmentToETABS.ExportAssignments(beamIdMapping);
            sb.AppendLine(beamAssignments);

            // Process brace assignments
            //string braceAssignments = _braceAssignmentToETABS.ExportAssignments(braceIdMapping);
            //sb.AppendLine(braceAssignments);

            return sb.ToString();
        }
    }
}
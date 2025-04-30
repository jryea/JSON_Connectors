using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using ETABS.ToETABS.Elements.AreaAssignment;
using ETABS.ToETABS.Elements.AreaConnectivity;
using System.Text;
using System;
using System.Collections.Generic;

namespace ETABS.ToETABS.Elements
{
    // Coordinator class for converting area elements (walls, floors) to ETABS E2K format
    public class AreaElementsToETABS
    {
        private readonly WallConnectivityToETABS _wallConnectivityToETABS;
        private readonly FloorConnectivityToETABS _floorConnectivityToETABS;

        private readonly WallAssignmentToETABS _wallAssignmentToETABS;
        private readonly FloorAssignmentToETABS _floorAssignmentToETABS;

        // Constructor that takes a PointCoordinatesToETABS instance
        public AreaElementsToETABS(PointCoordinatesToETABS pointCoordinates, List<string> validStoryNames)
        {
            if (pointCoordinates == null)
                throw new ArgumentNullException(nameof(pointCoordinates));

            // Initialize specialized converters with the point coordinates instance
            _wallConnectivityToETABS = new WallConnectivityToETABS(pointCoordinates);
            _floorConnectivityToETABS = new FloorConnectivityToETABS(pointCoordinates);

            _wallAssignmentToETABS = new WallAssignmentToETABS(validStoryNames);
            _floorAssignmentToETABS = new FloorAssignmentToETABS(validStoryNames);
        }

        // Converts area elements to E2K format
        public string ConvertToE2K(
            ElementContainer elements,
            ModelLayoutContainer layout,
            PropertiesContainer properties)
        {
            StringBuilder sb = new StringBuilder();

            // Set data for connectivity converters
            _wallConnectivityToETABS.SetWalls(elements.Walls);
            _floorConnectivityToETABS.SetFloors(elements.Floors);

            // Set data for assignment converters
            _wallAssignmentToETABS.SetData(elements.Walls, layout.Levels, properties.WallProperties);
            _floorAssignmentToETABS.SetData(elements.Floors, layout.Levels, properties.FloorProperties);

            // Process all area connectivities
            sb.AppendLine("$ AREA CONNECTIVITIES");

            // Process wall connectivities
            string wallConnectivities = _wallConnectivityToETABS.ExportConnectivities();
            sb.AppendLine(wallConnectivities);

            // Process floor connectivities
            string floorConnectivities = _floorConnectivityToETABS.ExportConnectivities();
            sb.AppendLine(floorConnectivities);

            sb.AppendLine();

            // Process all area assignments
            sb.AppendLine("$ AREA ASSIGNS");

            // Get ID mappings from connectivity converters
            var wallIdMapping = _wallConnectivityToETABS.GetIdMapping();
            var floorIdMapping = _floorConnectivityToETABS.GetIdMapping();

            // Process wall assignments
            string wallAssignments = _wallAssignmentToETABS.ExportAssignments(wallIdMapping);
            sb.AppendLine(wallAssignments);

            // Process floor assignments
            string floorAssignments = _floorAssignmentToETABS.ExportAssignments(floorIdMapping);
            sb.AppendLine(floorAssignments);

            return sb.ToString();
        }
    }
}
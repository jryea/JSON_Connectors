using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Utils = Core.Utilities.Utilities;

namespace ETABS.Export.Elements
{
    /// <summary>
    /// Exports area elements (walls, floors) for the E2K file format, handling both
    /// connectivities and assignments together to ensure consistency
    /// </summary>
    public class AreaElementsExport
    {
        // Store area IDs for mapping between connectivities and assignments
        private readonly Dictionary<string, string> _wallIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _floorIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<Point2D, string> _pointMapping;

        /// <summary>
        /// Constructor that takes a point mapping dictionary
        /// </summary>
        /// <param name="pointMapping">Dictionary mapping points to their IDs</param>
        public AreaElementsExport(Dictionary<Point2D, string> pointMapping)
        {
            _pointMapping = pointMapping ?? new Dictionary<Point2D, string>();
        }

        /// <summary>
        /// Processes the structural elements and creates both connectivities and assignments sections
        /// </summary>
        /// <param name="elements">Collection of structural elements</param>
        /// <param name="levels">Collection of levels</param>
        /// <param name="wallProperties">Collection of wall properties</param>
        /// <param name="floorProperties">Collection of floor properties</param>
        /// <returns>E2K format text for area elements (both connectivities and assignments)</returns>
        public string ConvertToE2K(
            ElementContainer elements,
            IEnumerable<Level> levels,
            IEnumerable<WallProperties> wallProperties,
            IEnumerable<FloorProperties> floorProperties)
        {
            StringBuilder sb = new StringBuilder();

            // First process the connectivities
            string connectivitiesSection = ProcessAreaConnectivities(elements);
            sb.AppendLine(connectivitiesSection);
            sb.AppendLine();

            // Then process the assignments using the same mappings
            string assignmentsSection = ProcessAreaAssignments(elements, levels, wallProperties, floorProperties);
            sb.AppendLine(assignmentsSection);

            return sb.ToString();
        }

        #region Connectivities Methods

        /// <summary>
        /// Processes area connectivities for walls and floors
        /// </summary>
        /// <param name="elements">Collection of structural elements</param>
        /// <returns>E2K format text for area connectivities</returns>
        private string ProcessAreaConnectivities(ElementContainer elements)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Area Connectivities Header
            sb.AppendLine("$ AREA CONNECTIVITIES");

            // Process Walls
            if (elements.Walls != null && elements.Walls.Count > 0)
            {
                for (int i = 0; i < elements.Walls.Count; i++)
                {
                    Wall wall = elements.Walls[i];
                    if (wall.Points != null && wall.Points.Count >= 3)
                    {
                        // Use consistent naming convention (W1, W2, etc.)
                        string wallId = $"W{i + 1}";

                        // Store the mapping for later use in assignments
                        _wallIdMapping[wall.Id] = wallId;

                        string wallLine = FormatWallConnectivity(wall, wallId);
                        sb.AppendLine(wallLine);
                    }
                }
            }

            // Process Floors
            if (elements.Floors != null && elements.Floors.Count > 0)
            {
                for (int i = 0; i < elements.Floors.Count; i++)
                {
                    Floor floor = elements.Floors[i];
                    if (floor.Points != null && floor.Points.Count >= 3)
                    {
                        // Use consistent naming convention (F1, F2, etc.)
                        string floorId = $"F{i + 1}";

                        // Store the mapping for later use in assignments
                        _floorIdMapping[floor.Id] = floorId;

                        string floorLine = FormatFloorConnectivity(floor, floorId);
                        sb.AppendLine(floorLine);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a wall connectivity for the E2K file
        /// </summary>
        /// <param name="wall">Wall element</param>
        /// <param name="wallId">Consistent wall ID to use</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatWallConnectivity(Wall wall, string wallId)
        {
            StringBuilder sb = new StringBuilder();

            // Get the number of vertices
            int numVertices = wall.Points.Count;

            sb.Append($"  AREA \"{wallId}\" PANEL {numVertices}");

            // Add point references
            foreach (var point in wall.Points)
            {
                string pointId = Utils.GetPointId(point, _pointMapping);
                sb.Append($" \"{pointId}\"");
            }

            // Repeat the first point to close the polygon if not already closed
            if (wall.Points.Count > 0 && !Utils.ArePointsEqual(wall.Points[0], wall.Points[wall.Points.Count - 1]))
            {
                string firstPointId = Utils.GetPointId(wall.Points[0], _pointMapping);
                sb.Append($" \"{firstPointId}\"");
            }

            // Add additional parameters (fixed values for now)
            sb.Append(" 1 1 0 0");

            return sb.ToString();
        }

        /// <summary>
        /// Formats a floor connectivity for the E2K file
        /// </summary>
        /// <param name="floor">Floor element</param>
        /// <param name="floorId">Consistent floor ID to use</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatFloorConnectivity(Floor floor, string floorId)
        {
            StringBuilder sb = new StringBuilder();

            // Get the number of vertices
            int numVertices = floor.Points.Count;

            sb.Append($"  AREA \"{floorId}\" FLOOR {numVertices}");

            // Add point references
            foreach (var point in floor.Points)
            {
                string pointId = Utils.GetPointId(point, _pointMapping);
                sb.Append($" \"{pointId}\"");
            }

            // Add zeros for additional parameters (one for each vertex)
            for (int i = 0; i < numVertices; i++)
            {
                sb.Append(" 0");
            }

            return sb.ToString();
        }

        #endregion

        #region Assignments Methods

        /// <summary>
        /// Processes area assignments for walls and floors
        /// </summary>
        /// <param name="elements">Collection of structural elements</param>
        /// <param name="levels">Collection of levels</param>
        /// <param name="wallProperties">Collection of wall properties</param>
        /// <param name="floorProperties">Collection of floor properties</param>
        /// <returns>E2K format text for area assignments</returns>
        private string ProcessAreaAssignments(
            ElementContainer elements,
            IEnumerable<Level> levels,
            IEnumerable<WallProperties> wallProperties,
            IEnumerable<FloorProperties> floorProperties)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Area Assignments Header
            sb.AppendLine("$ AREA ASSIGNS");

            // Process wall assignments
            if (elements.Walls != null && elements.Walls.Count > 0)
            {
                // Get sorted list of levels for consistent assignment order
                var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

                foreach (var wall in elements.Walls)
                {
                    if (!_wallIdMapping.ContainsKey(wall.Id))
                        continue;

                    // Get the E2K area ID from the mapping
                    string areaId = _wallIdMapping[wall.Id];

                    // Get the wall properties
                    WallProperties wallProps = Utils.FindWallProperties(wallProperties, wall.PropertiesId);
                    if (wallProps == null)
                        continue;

                    // Generate story names for a multi-story wall going from the base to top level
                    // Skip the base level (first level) as per usual ETABS convention
                    for (int i = 1; i < sortedLevels.Count; i++)
                    {
                        string storyName = $"Story{i}";

                        string areaAssign = FormatAreaAssign(
                            areaId,
                            storyName,
                            wallProps.Name,
                            "DEFAULT",
                            "Yes",
                            "MIDDLE",
                            "No");

                        sb.AppendLine(areaAssign);
                    }
                }
            }

            // Process floor assignments
            if (elements.Floors != null && elements.Floors.Count > 0)
            {
                foreach (var floor in elements.Floors)
                {
                    if (!_floorIdMapping.ContainsKey(floor.Id))
                        continue;

                    // Get the E2K area ID from the mapping
                    string areaId = _floorIdMapping[floor.Id];

                    // Get the floor properties
                    FloorProperties floorProps = Utils.FindFloorProperties(floorProperties, floor.FloorPropertiesId);
                    if (floorProps == null)
                        continue;

                    // Find the level this floor belongs to
                    Level floorLevel = Utils.FindLevel(levels, floor.LevelId);
                    if (floorLevel == null)
                        continue;

                    // Create an area assign entry for this floor at its level
                    string areaAssign = FormatAreaAssign(
                        areaId,
                        floorLevel.Name,
                        floorProps.Name,
                        "DEFAULT",
                        "No",
                        "MIDDLE",
                        "No");

                    sb.AppendLine(areaAssign);

                    // Add diaphragm information if present
                    if (!string.IsNullOrEmpty(floor.DiaphragmId))
                    {
                        string diaphragmAssign = $"  AREAASSIGN \"{areaId}\" \"{floorLevel.Name}\" DIAPH \"{floor.DiaphragmId}\"";
                        sb.AppendLine(diaphragmAssign);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats an area assignment line for E2K format
        /// </summary>
        /// <param name="areaId">ID of the area</param>
        /// <param name="story">Story or level name</param>
        /// <param name="section">Section or property name</param>
        /// <param name="meshType">Mesh type (e.g., "DEFAULT")</param>
        /// <param name="addRestraint">Add restraint flag (e.g., "Yes" or "No")</param>
        /// <param name="cardinalPoint">Cardinal point (e.g., "MIDDLE")</param>
        /// <param name="transformStiffness">Transform stiffness flag (e.g., "Yes" or "No")</param>
        /// <returns>Formatted area assignment line in E2K format</returns>
        private string FormatAreaAssign(
            string areaId,
            string story,
            string section,
            string meshType,
            string addRestraint,
            string cardinalPoint,
            string transformStiffness)
        {
            return $"  AREAASSIGN \"{areaId}\" \"{story}\" SECTION \"{section}\" OBJMESHTYPE \"{meshType}\" " +
                   $"ADDRESTRAINT \"{addRestraint}\" CARDINALPOINT \"{cardinalPoint}\" " +
                   $"TRANSFORMSTIFFNESSFOROFFSETS \"{transformStiffness}\"";
        }

        #endregion
    }
}
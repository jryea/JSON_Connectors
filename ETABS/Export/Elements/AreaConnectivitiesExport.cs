using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Utils = Core.Utilities.Utilities;

namespace ETABS.Export.Elements
{
    /// <summary>
    /// Exports area connectivities for the E2K file format
    /// </summary>
    public class AreaConnectivitiesExport
    {
        private readonly Dictionary<Point2D, string> _pointMapping;

        /// <summary>
        /// Constructor that takes a point mapping dictionary
        /// </summary>
        /// <param name="pointMapping">Dictionary mapping points to their IDs</param>
        public AreaConnectivitiesExport(Dictionary<Point2D, string> pointMapping)
        {
            _pointMapping = pointMapping ?? new Dictionary<Point2D, string>();
        }

        /// <summary>
        /// Converts structural elements to E2K format text for area connectivities
        /// </summary>
        /// <param name="elements">Collection of structural elements from the model</param>
        /// <returns>E2K format text for area connectivities</returns>
        public string ConvertToE2K(ElementContainer elements)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Area Connectivities Header
            sb.AppendLine("$ AREA CONNECTIVITIES");

            // Process Walls
            int wallCounter = 1;
            if (elements.Walls != null && elements.Walls.Count > 0)
            {
                foreach (var wall in elements.Walls)
                {
                    if (wall.Points != null && wall.Points.Count >= 3)
                    {
                        string wallLine = FormatWallLine(wall, wallCounter);
                        sb.AppendLine(wallLine);
                        wallCounter++;
                    }
                }
            }

            // Process Floors
            int floorCounter = 1;
            if (elements.Floors != null && elements.Floors.Count > 0)
            {
                foreach (var floor in elements.Floors)
                {
                    if (floor.Points != null && floor.Points.Count >= 3)
                    {
                        string floorLine = FormatFloorLine(floor, floorCounter);
                        sb.AppendLine(floorLine);
                        floorCounter++;
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a wall for the E2K file
        /// </summary>
        /// <param name="wall">Wall element</param>
        /// <param name="counter">Wall counter for naming</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatWallLine(Wall wall, int counter)
        {
            StringBuilder sb = new StringBuilder();

            // Get the number of vertices (without duplicating the first point in the count)
            int numVertices = wall.Points.Count;

            sb.Append($"  AREA \"W{counter}\" PANEL {numVertices}");

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
        /// Formats a floor for the E2K file
        /// </summary>
        /// <param name="floor">Floor element</param>
        /// <param name="counter">Floor counter for naming</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatFloorLine(Floor floor, int counter)
        {
            StringBuilder sb = new StringBuilder();

            // Get the number of vertices (without duplicating the first point in the count)
            int numVertices = floor.Points.Count;

            sb.Append($"  AREA \"F{counter}\"  FLOOR {numVertices}");

            // Add point references
            foreach (var point in floor.Points)
            {
                string pointId = Utils.GetPointId(point, _pointMapping);
                sb.Append($"  \"{pointId}\"");
            }

            // Add zeros for additional parameters (one for each vertex)
            for (int i = 0; i < numVertices; i++)
            {
                sb.Append(" 0");
            }

            return sb.ToString();
        }

       
    }
}
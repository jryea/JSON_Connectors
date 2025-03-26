using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Utils = Core.Utilities.Utilities;

namespace ETABS.Export.Elements
{
    /// <summary>
    /// Converts Core Area objects to ETABS E2K format text for area assignments
    /// </summary>
    public class AreaAssignsExport
    {
        /// <summary>
        /// Converts a collection of Wall objects to E2K format text for area assignments
        /// </summary>
        /// <param name="walls">Collection of Wall objects</param>
        /// <param name="levels">Collection of Level objects</param>
        /// <param name="properties">Wall properties</param>
        /// <returns>E2K format text for wall area assignments</returns>
        public string ConvertWallsToE2K(IEnumerable<Wall> walls, IEnumerable<Level> levels, IEnumerable<WallProperties> properties)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Area Assignments Section Header
            sb.AppendLine("$ AREA ASSIGNS");

            foreach (var wall in walls)
            {
                // Get the wall properties
                WallProperties wallProps = Utils.FindWallProperties(properties, wall.PropertiesId);
                if (wallProps == null)
                    continue;

                // For each level, create an area assign entry
                foreach (var level in levels)
                {
                    string areaAssign = FormatAreaAssign(wall.Id, level.Name, wallProps.Name, "DEFAULT", "Yes", "MIDDLE", "No");
                    sb.AppendLine(areaAssign);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Converts a collection of Floor objects to E2K format text for area assignments
        /// </summary>
        /// <param name="floors">Collection of Floor objects</param>
        /// <param name="levels">Collection of Level objects</param>
        /// <param name="properties">Floor properties</param>
        /// <returns>E2K format text for floor area assignments</returns>
        public string ConvertFloorsToE2K(IEnumerable<Floor> floors, IEnumerable<Level> levels, IEnumerable<FloorProperties> properties)
        {
            StringBuilder sb = new StringBuilder();

            // If there are no floor assignments yet, add the header
            if (sb.Length == 0)
                sb.AppendLine("$ AREA ASSIGNS");

            foreach (var floor in floors)
            {
                // Get the floor properties
                FloorProperties floorProps = Utils.FindFloorProperties(properties, floor.FloorPropertiesId);
                if (floorProps == null)
                    continue;

                // Find the level this floor belongs to
                Level floorLevel = Utils.FindLevel(levels, floor.LevelId);
                if (floorLevel == null)
                    continue;

                // Create an area assign entry for this floor at its level
                string areaAssign = FormatAreaAssign(floor.Id, floorLevel.Name, floorProps.Name, "DEFAULT", "No", "MIDDLE", "No");
                sb.AppendLine(areaAssign);

                // Add diaphragm information if present
                if (!string.IsNullOrEmpty(floor.DiaphragmId))
                {
                    string diaphragmAssign = $"  AREAASSIGN \"{floor.Id}\" \"{floorLevel.Name}\" DIAPH \"{floor.DiaphragmId}\"";
                    sb.AppendLine(diaphragmAssign);
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
        private string FormatAreaAssign(string areaId, string story, string section, string meshType,
            string addRestraint, string cardinalPoint, string transformStiffness)
        {
            return $"  AREAASSIGN \"{areaId}\" \"{story}\" SECTION \"{section}\" OBJMESHTYPE \"{meshType}\" " +
                   $"ADDRESTRAINT \"{addRestraint}\" CARDINALPOINT \"{cardinalPoint}\" " +
                   $"TRANSFORMSTIFFNESSFOROFFSETS \"{transformStiffness}\"";
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;

namespace ETABS.ToETABS.Elements
{
    /// <summary>
    /// Converts area elements (walls, floors) to ETABS E2K format
    /// </summary>
    public class AreaElementsToETABS
    {
        private readonly PointCoordinatesToETABS _pointCoordinates;

        /// <summary>
        /// Initializes a new instance of the AreaElementsToETABS class
        /// </summary>
        /// <param name="pointCoordinates">The point coordinates manager instance</param>
        public AreaElementsToETABS(PointCoordinatesToETABS pointCoordinates)
        {
            _pointCoordinates = pointCoordinates ?? throw new ArgumentNullException(nameof(pointCoordinates));
        }

        /// <summary>
        /// Converts area elements to E2K format
        /// </summary>
        /// <param name="elements">The structural elements to convert</param>
        /// <param name="levels">The collection of levels</param>
        /// <param name="wallProperties">The collection of wall properties</param>
        /// <param name="floorProperties">The collection of floor properties</param>
        /// <returns>E2K formatted area elements</returns>
        public string ConvertToE2K(
            ElementContainer elements,
            IEnumerable<Level> levels,
            IEnumerable<WallProperties> wallProperties,
            IEnumerable<FloorProperties> floorProperties)
        {
            StringBuilder sb = new StringBuilder();

            // Process area connectivities
            sb.AppendLine("$ AREA CONNECTIVITIES");

            // Process walls
            if (elements.Walls != null && elements.Walls.Count > 0)
            {
                foreach (var wall in elements.Walls)
                {
                    if (wall.Points == null || wall.Points.Count < 3)
                        continue;

                    // Get point IDs for all wall vertices
                    List<string> pointIds = new List<string>();
                    foreach (var point in wall.Points)
                    {
                        string pointId = _pointCoordinates.GetOrCreatePointId(point);
                        pointIds.Add(pointId);
                    }

                    // Format wall connectivity
                    sb.AppendLine(FormatAreaConnectivity($"W{wall.Id}", "WALL", pointIds));
                }
            }

            // Process floors
            if (elements.Floors != null && elements.Floors.Count > 0)
            {
                foreach (var floor in elements.Floors)
                {
                    if (floor.Points == null || floor.Points.Count < 3)
                        continue;

                    // Get point IDs for all floor vertices
                    List<string> pointIds = new List<string>();
                    foreach (var point in floor.Points)
                    {
                        string pointId = _pointCoordinates.GetOrCreatePointId(point);
                        pointIds.Add(pointId);
                    }

                    // Format floor connectivity
                    sb.AppendLine(FormatAreaConnectivity($"F{floor.Id}", "FLOOR", pointIds));
                }
            }

            sb.AppendLine();

            // Process area assignments
            sb.AppendLine("$ AREA ASSIGNS");

            // Process wall assignments
            if (elements.Walls != null && elements.Walls.Count > 0)
            {
                foreach (var wall in elements.Walls)
                {
                    if (wall.Points == null || wall.Points.Count < 3)
                        continue;

                    // Find levels
                    string baseStory = GetStoryName(wall.BaseLevelId, levels);
                    string topStory = GetStoryName(wall.TopLevelId, levels);

                    // Find wall properties
                    string propertyName = GetWallPropertyName(wall.PropertiesId, wallProperties);

                    // Format wall assignment
                    sb.AppendLine(FormatWallAssignment($"W{wall.Id}", baseStory, topStory, propertyName));
                }
            }

            // Process floor assignments
            if (elements.Floors != null && elements.Floors.Count > 0)
            {
                foreach (var floor in elements.Floors)
                {
                    if (floor.Points == null || floor.Points.Count < 3)
                        continue;

                    // Find level
                    string story = GetStoryName(floor.LevelId, levels);

                    // Find floor properties
                    string propertyName = GetFloorPropertyName(floor.FloorPropertiesId, floorProperties);

                    // Find diaphragm assignment
                    string diaphragm = string.IsNullOrEmpty(floor.DiaphragmId) ? "NONE" : floor.DiaphragmId;

                    // Format floor assignment
                    sb.AppendLine(FormatFloorAssignment($"F{floor.Id}", story, propertyName, diaphragm));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the story name for a level ID
        /// </summary>
        /// <param name="levelId">The level ID</param>
        /// <param name="levels">The collection of levels</param>
        /// <returns>The story name for ETABS</returns>
        private string GetStoryName(string levelId, IEnumerable<Level> levels)
        {
            if (string.IsNullOrEmpty(levelId) || levels == null)
                return "Story1"; // Default

            foreach (var level in levels)
            {
                if (level.Id == levelId)
                {
                    // Format story name
                    return level.Name.ToLower() == "base" ? "Base" : $"Story{level.Name}";
                }
            }

            return "Story1"; // Default if not found
        }

        /// <summary>
        /// Gets the wall property name for a property ID
        /// </summary>
        /// <param name="propertyId">The property ID</param>
        /// <param name="wallProperties">The collection of wall properties</param>
        /// <returns>The wall property name</returns>
        private string GetWallPropertyName(string propertyId, IEnumerable<WallProperties> wallProperties)
        {
            if (string.IsNullOrEmpty(propertyId) || wallProperties == null)
                return "Default"; // Default name

            foreach (var prop in wallProperties)
            {
                if (prop.Id == propertyId)
                    return prop.Name;
            }

            return "Default"; // Default if not found
        }

        /// <summary>
        /// Gets the floor property name for a property ID
        /// </summary>
        /// <param name="propertyId">The property ID</param>
        /// <param name="floorProperties">The collection of floor properties</param>
        /// <returns>The floor property name</returns>
        private string GetFloorPropertyName(string propertyId, IEnumerable<FloorProperties> floorProperties)
        {
            if (string.IsNullOrEmpty(propertyId) || floorProperties == null)
                return "Default"; // Default name

            foreach (var prop in floorProperties)
            {
                if (prop.Id == propertyId)
                    return prop.Name;
            }

            return "Default"; // Default if not found
        }

        /// <summary>
        /// Formats an area connectivity line for E2K format
        /// </summary>
        /// <param name="areaId">The area ID</param>
        /// <param name="areaType">The area type (WALL or FLOOR)</param>
        /// <param name="pointIds">The list of point IDs</param>
        /// <returns>The formatted E2K line</returns>
        private string FormatAreaConnectivity(string areaId, string areaType, List<string> pointIds)
        {
            // Format: AREA "W1" WALL "1" "2" "3" "4"
            return $"  AREA \"{areaId}\" {areaType} {string.Join(" ", pointIds.ConvertAll(id => $"\"{id}\""))}";
        }

        /// <summary>
        /// Formats a wall assignment line for E2K format
        /// </summary>
        /// <param name="areaId">The area ID</param>
        /// <param name="baseStory">The base story name</param>
        /// <param name="topStory">The top story name</param>
        /// <param name="propertyName">The wall property name</param>
        /// <returns>The formatted E2K line</returns>
        private string FormatWallAssignment(string areaId, string baseStory, string topStory, string propertyName)
        {
            // Format: AREAASSIGN "W1" "Story1" "Story2" SECTION "Wall1" AUTOMESH "YES"
            return $"  AREAASSIGN \"{areaId}\" \"{baseStory}\" \"{topStory}\" SECTION \"{propertyName}\" AUTOMESH \"YES\"";
        }

        /// <summary>
        /// Formats a floor assignment line for E2K format
        /// </summary>
        /// <param name="areaId">The area ID</param>
        /// <param name="story">The story name</param>
        /// <param name="propertyName">The floor property name</param>
        /// <param name="diaphragm">The diaphragm assignment</param>
        /// <returns>The formatted E2K line</returns>
        private string FormatFloorAssignment(string areaId, string story, string propertyName, string diaphragm)
        {
            // Format: AREAASSIGN "F1" "Story2" SECTION "Slab1" DIAPHRAGM "RIGID" AUTOMESH "YES"
            return $"  AREAASSIGN \"{areaId}\" \"{story}\" SECTION \"{propertyName}\" DIAPHRAGM \"{diaphragm}\" AUTOMESH \"YES\"";
        }
    }
}
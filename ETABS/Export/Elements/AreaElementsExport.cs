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
    // Exports area elements (walls, floors) for the E2K file format, handling both
  
    public class AreaElementsExport
    {
        // Store area IDs for mapping between connectivities and assignments
        private readonly Dictionary<string, string> _wallIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _floorIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<Point2D, string> _pointMapping;

        // Constructor that takes a point mapping dictionary
        public AreaElementsExport(Dictionary<Point2D, string> pointMapping)
        {
            _pointMapping = pointMapping ?? new Dictionary<Point2D, string>();
        }

        // Processes the structural elements and creates both connectivities and assignments sections
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

        // Processes area connectivities for walls and floors
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
                    if (wall.Points != null && wall.Points.Count >= 2)
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

        // Formats a wall connectivity for the E2K file
        private string FormatWallConnectivity(Wall wall, string wallId)
        {
            StringBuilder sb = new StringBuilder();

            // Get the number of vertices
            int numVertices = wall.Points.Count;

            // If there are only two points, duplicate them to form a closed polygon
            if (numVertices == 2)
            {
                wall.Points.Add(wall.Points[1]);
                wall.Points.Add(wall.Points[0]);
                numVertices = 4;
            }

            sb.Append($"  AREA \"{wallId}\" PANEL {numVertices}");

            // Add point references
            foreach (var point in wall.Points)
            {
                string pointId = Utils.GetPointId(point, _pointMapping);
                sb.Append($" \"{pointId}\"");
            }

            // Add additional parameters (fixed values for now)
            sb.Append(" 1 1 0 0");

            return sb.ToString();
        }

        // Formats a floor connectivity for the E2K file
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

        // Processes area assignments for walls and floors
        private string ProcessAreaAssignments(
            ElementContainer elements,
            IEnumerable<Level> levels,
            IEnumerable<WallProperties> wallProperties,
            IEnumerable<FloorProperties> floorProperties)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Area Assignments Header
            sb.AppendLine("$ AREA ASSIGNS");

            // Convert levels to a sorted list for easier access
            var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

            // Create a dictionary to map level IDs to level objects
            var levelDict = sortedLevels.ToDictionary(l => l.Id, l => l);

            // Process wall assignments
            if (elements.Walls != null && elements.Walls.Count > 0)
            {
                foreach (var wall in elements.Walls)
                {
                    if (!_wallIdMapping.ContainsKey(wall.Id))
                        continue;

                    // Get the E2K area ID from the mapping
                    string areaId = _wallIdMapping[wall.Id];

                    // Get the wall properties
                    var wallProp = wallProperties.FirstOrDefault(wp => wp.Id == wall.PropertiesId);
                    if (wallProp == null)
                        continue;

                    // Find the base and top levels for this wall
                    if (!string.IsNullOrEmpty(wall.BaseLevelId) && !string.IsNullOrEmpty(wall.TopLevelId))
                    {
                        Level baseLevel = null;
                        Level topLevel = null;

                        // Try to find the levels in our dictionary
                        levelDict.TryGetValue(wall.BaseLevelId, out baseLevel);
                        levelDict.TryGetValue(wall.TopLevelId, out topLevel);

                        if (baseLevel != null && topLevel != null)
                        {
                            // Find the level directly above the base level
                            Level nextLevel = sortedLevels.FirstOrDefault(l => l.Elevation > baseLevel.Elevation);

                            // Find all levels between (and including) nextLevel and top levels
                            foreach (var level in sortedLevels)
                            {
                                // Only include levels from nextLevel to top (inclusive)
                                if (level.Elevation >= nextLevel.Elevation && level.Elevation <= topLevel.Elevation)
                                {
                                    // Add "Story" prefix to level name
                                    string storyName = $"Story{level.Name}";

                                    string areaAssign = FormatAreaAssign(
                                        areaId,
                                        storyName,
                                        wallProp.Name,
                                        "DEFAULT",
                                        "Yes",
                                        "MIDDLE",
                                        "No");

                                    sb.AppendLine(areaAssign);
                                }
                            }
                        }
                        else
                        {
                            // If we can't find the levels, assign to all levels as a fallback
                            foreach (var level in sortedLevels)
                            {
                                // Add "Story" prefix to level name
                                string storyName = $"Story{level.Name}";

                                string areaAssign = FormatAreaAssign(
                                    areaId,
                                    storyName,
                                    wallProp.Name,
                                    "DEFAULT",
                                    "Yes",
                                    "MIDDLE",
                                    "No");

                                sb.AppendLine(areaAssign);
                            }
                        }
                    }
                    else
                    {
                        // If no base/top levels specified, assign to all levels as fallback
                        foreach (var level in sortedLevels)
                        {
                            // Add "Story" prefix to level name
                            string storyName = $"Story{level.Name}";

                            string areaAssign = FormatAreaAssign(
                                areaId,
                                storyName,
                                wallProp.Name,
                                "DEFAULT",
                                "Yes",
                                "MIDDLE",
                                "No");

                            sb.AppendLine(areaAssign);
                        }
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
                    var floorProp = floorProperties?.FirstOrDefault(fp => fp.Id == floor.FloorPropertiesId);
                    string sectionName = floorProp?.Name ?? "Slab1"; // Use default name if properties not found

                    // Find the level this floor belongs to
                    Level floorLevel = null;
                    if (!string.IsNullOrEmpty(floor.LevelId))
                    {
                        levelDict.TryGetValue(floor.LevelId, out floorLevel);
                    }

                    // If level not found, try to estimate based on points
                    if (floorLevel == null && floor.Points != null && floor.Points.Count > 0)
                    {
                        // Get the Z coordinate from the floor's point (assuming all points have same Z)
                        // This is a simplification - in a real implementation,
                        // we'd need to consider the actual Z coordinates
                        floorLevel = sortedLevels.FirstOrDefault();
                    }

                    // If we still don't have a level, use the first level as default
                    if (floorLevel == null && sortedLevels.Count > 0)
                    {
                        floorLevel = sortedLevels[0];
                    }

                    if (floorLevel != null)
                    {
                        // Add "Story" prefix to level name
                        string storyName = $"Story{floorLevel.Name}";

                        // Create an area assign entry for this floor at its level
                        string areaAssign = FormatAreaAssign(
                            areaId,
                            storyName,
                            sectionName,
                            "DEFAULT",
                            "No",
                            "MIDDLE",
                            "No");

                        sb.AppendLine(areaAssign);

                        // Add diaphragm information if present
                        if (!string.IsNullOrEmpty(floor.DiaphragmId))
                        {
                            string diaphragmAssign = $"  AREAASSIGN \"{areaId}\" \"{storyName}\" DIAPH \"{floor.DiaphragmId}\"";
                            sb.AppendLine(diaphragmAssign);
                        }
                        else
                        {
                            // Add default diaphragm if none specified
                            string diaphragmAssign = $"  AREAASSIGN \"{areaId}\" \"{storyName}\" DIAPH \"D1\"";
                            sb.AppendLine(diaphragmAssign);
                        }
                    }
                }
            }

            return sb.ToString();
        }

        // Formats an area assignment line for E2K format
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
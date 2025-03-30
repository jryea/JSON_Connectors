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
   
    // Exports line elements (beams, columns, braces) for the E2K file format
    public class LineElementsExport
    {
        // Store line element IDs for mapping between connectivities and assignments
        private readonly Dictionary<string, string> _beamIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _columnIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _braceIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<Point2D, string> _pointMapping;
    
        // Constructor that takes a point mapping dictionary
        public LineElementsExport(Dictionary<Point2D, string> pointMapping)
        {
            _pointMapping = pointMapping ?? new Dictionary<Point2D, string>();
        }

        // Processes the structural elements and creates both connectivities and assignments sections
        public string ConvertToE2K(
            ElementContainer elements,
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties)
        {
            StringBuilder sb = new StringBuilder();

            // First process the connectivities
            string connectivitiesSection = ProcessLineConnectivities(elements);
            sb.AppendLine(connectivitiesSection);
            sb.AppendLine();

            // Then process the assignments using the same mappings
            string assignmentsSection = ProcessLineAssignments(elements, levels, frameProperties);
            sb.AppendLine(assignmentsSection);

            return sb.ToString();
        }

       
      
        // Processes line connectivities for beams, columns, and braces
        private string ProcessLineConnectivities(ElementContainer elements)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Line Connectivities Header
            sb.AppendLine("$ LINE CONNECTIVITIES");

            // Process Beams
            if (elements.Beams != null && elements.Beams.Count > 0)
            {
                for (int i = 0; i < elements.Beams.Count; i++)
                {
                    Beam beam = elements.Beams[i];
                    if (beam.StartPoint != null && beam.EndPoint != null)
                    {
                        // Use consistent naming convention (B1, B2, etc.)
                        string beamId = $"B{i + 1}";

                        // Store the mapping for later use in assignments
                        _beamIdMapping[beam.Id] = beamId;

                        string beamLine = FormatBeamConnectivity(beam, beamId);
                        sb.AppendLine(beamLine);
                    }
                }
            }

            // Process Columns
            if (elements.Columns != null && elements.Columns.Count > 0)
            {
                for (int i = 0; i < elements.Columns.Count; i++)
                {
                    Column column = elements.Columns[i];
                    if (column.StartPoint != null && column.EndPoint != null)
                    {
                        // Use consistent naming convention (C1, C2, etc.)
                        string columnId = $"C{i + 1}";

                        // Store the mapping for later use in assignments
                        _columnIdMapping[column.Id] = columnId;

                        string columnLine = FormatColumnConnectivity(column, columnId);
                        sb.AppendLine(columnLine);
                    }
                }
            }

            // Process Braces
            if (elements.Braces != null && elements.Braces.Count > 0)
            {
                for (int i = 0; i < elements.Braces.Count; i++)
                {
                    Brace brace = elements.Braces[i];
                    if (brace.StartPoint != null && brace.EndPoint != null)
                    {
                        // Use consistent naming convention (BR1, BR2, etc.)
                        string braceId = $"BR{i + 1}";

                        // Store the mapping for later use in assignments
                        _braceIdMapping[brace.Id] = braceId;

                        string braceLine = FormatBraceConnectivity(brace, braceId);
                        sb.AppendLine(braceLine);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a beam connectivity for the E2K file
        /// </summary>
        /// <param name="beam">Beam element</param>
        /// <param name="beamId">Consistent beam ID to use</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatBeamConnectivity(Beam beam, string beamId)
        {
            string startPointId = Utils.GetPointId(beam.StartPoint, _pointMapping);
            string endPointId = Utils.GetPointId(beam.EndPoint, _pointMapping);

            // Format: LINE  "B1"  BEAM  "1"  "2"  0
            return $"  LINE  \"{beamId}\"  BEAM  \"{startPointId}\"  \"{endPointId}\"  0";
        }

        /// <summary>
        /// Formats a column connectivity for the E2K file
        /// </summary>
        /// <param name="column">Column element</param>
        /// <param name="columnId">Consistent column ID to use</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatColumnConnectivity(Column column, string columnId)
        {
            string startPointId = Utils.GetPointId(column.StartPoint, _pointMapping);
            string endPointId = Utils.GetPointId(column.EndPoint, _pointMapping);

            // Format: LINE  "C1"  COLUMN  "9"  "9"  1
            return $"  LINE  \"{columnId}\"  COLUMN  \"{startPointId}\"  \"{endPointId}\"  1";
        }

        /// <summary>
        /// Formats a brace connectivity for the E2K file
        /// </summary>
        /// <param name="brace">Brace element</param>
        /// <param name="braceId">Consistent brace ID to use</param>
        /// <returns>Formatted string for the E2K file</returns>
        private string FormatBraceConnectivity(Brace brace, string braceId)
        {
            string startPointId = Utils.GetPointId(brace.StartPoint, _pointMapping);
            string endPointId = Utils.GetPointId(brace.EndPoint, _pointMapping);

            // Format: LINE  "BR1"  BRACE  "3"  "4"  0
            return $"  LINE  \"{braceId}\"  BRACE  \"{startPointId}\"  \"{endPointId}\"  0";
        }

        /// <summary>
        /// Processes line assignments for beams, columns, and braces
        /// </summary>
        /// <param name="elements">Collection of structural elements</param>
        /// <param name="levels">Collection of levels</param>
        /// <param name="frameProperties">Collection of frame properties</param>
        /// <returns>E2K format text for line assignments</returns>
        private string ProcessLineAssignments(
            ElementContainer elements,
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Line Assignments Header
            sb.AppendLine("$ LINE ASSIGNS");

            // Process beam assignments
            if (elements.Beams != null && elements.Beams.Count > 0)
            {
                foreach (var beam in elements.Beams)
                {
                    if (!_beamIdMapping.ContainsKey(beam.Id))
                        continue;

                    // Get the E2K line ID from the mapping
                    string lineId = _beamIdMapping[beam.Id];

                    // Get the beam properties
                    var frameProp = frameProperties.FirstOrDefault(fp => fp.Id == beam.FramePropertiesId);
                    if (frameProp == null)
                        continue;

                    // Find the level this beam belongs to
                    Level beamLevel = Utils.FindLevel(levels, beam.LevelId);
                    if (beamLevel == null)
                        continue;

                    // Create a line assign entry for this beam at its level
                    string lineAssign = FormatLineAssign(
                        lineId,
                        beamLevel.Name,
                        frameProp.Name,
                        24, // Default value for max station spacing
                        "YES", // Auto mesh
                        "YES" // Mesh at intersections
                    );

                    sb.AppendLine(lineAssign);
                }
            }

            // Process column assignments
            if (elements.Columns != null && elements.Columns.Count > 0)
            {
                foreach (var column in elements.Columns)
                {
                    if (!_columnIdMapping.ContainsKey(column.Id))
                        continue;

                    // Get the E2K line ID from the mapping
                    string lineId = _columnIdMapping[column.Id];

                    // Get the column properties
                    var frameProp = frameProperties.FirstOrDefault(fp => fp.Id == column.FramePropertiesId);
                    if (frameProp == null)
                        continue;

                    // Find the base level for this column
                    Level baseLevel = Utils.FindLevel(levels, column.BaseLevelId);
                    if (baseLevel == null)
                        continue;

                    // Determine end releases - columns usually have "M2J M3J" at base and "PINNED" above
                    string release = "M2J M3J"; // Base level typically has moment releases at the bottom

                    // Format the column assignment
                    string lineAssign = FormatColumnAssign(
                        lineId,
                        baseLevel.Name,
                        frameProp.Name,
                        release,
                        3, // Minimum number of stations
                        "YES", // Auto mesh
                        "YES" // Mesh at intersections
                    );

                    sb.AppendLine(lineAssign);

                    // If column extends to higher levels, create assignments for those as well
                    // For upper stories, change release to "PINNED"
                    if (!string.IsNullOrEmpty(column.TopLevelId) && column.TopLevelId != column.BaseLevelId)
                    {
                        Level topLevel = Utils.FindLevel(levels, column.TopLevelId);
                        if (topLevel != null)
                        {
                            // Create assignments for all intermediate levels between base and top
                            foreach (var level in levels)
                            {
                                // Skip the base level (already processed) and levels outside the column span
                                if (level.Id == baseLevel.Id || level.Elevation <= baseLevel.Elevation || level.Elevation > topLevel.Elevation)
                                    continue;

                                string upperLevelAssign = FormatColumnAssign(
                                    lineId,
                                    level.Name,
                                    frameProp.Name,
                                    "PINNED", // Upper levels typically have pinned connections
                                    3, // Minimum number of stations
                                    "YES", // Auto mesh
                                    "YES" // Mesh at intersections
                                );

                                sb.AppendLine(upperLevelAssign);
                            }
                        }
                    }
                }
            }

            // Process brace assignments
            if (elements.Braces != null && elements.Braces.Count > 0)
            {
                foreach (var brace in elements.Braces)
                {
                    if (!_braceIdMapping.ContainsKey(brace.Id))
                        continue;

                    // Get the E2K line ID from the mapping
                    string lineId = _braceIdMapping[brace.Id];

                    // Get the brace properties
                    var frameProp = frameProperties.FirstOrDefault(fp => fp.Id == brace.FramePropertiesId);
                    if (frameProp == null)
                        continue;

                    // Find the base level for this brace
                    Level baseLevel = Utils.FindLevel(levels, brace.BaseLevelId);
                    if (baseLevel == null)
                        continue;

                    // Format the brace assignment
                    string lineAssign = FormatBraceAssign(
                        lineId,
                        baseLevel.Name,
                        frameProp.Name,
                        "PINNED", // Braces typically have pinned connections
                        24, // Default value for max station spacing
                        "YES", // Auto mesh
                        "YES" // Mesh at intersections
                    );

                    sb.AppendLine(lineAssign);

                    // If brace extends to higher levels, create assignments for those as well
                    if (!string.IsNullOrEmpty(brace.TopLevelId) && brace.TopLevelId != brace.BaseLevelId)
                    {
                        Level topLevel = Utils.FindLevel(levels, brace.TopLevelId);
                        if (topLevel != null)
                        {
                            // Create assignments for all intermediate levels between base and top
                            foreach (var level in levels)
                            {
                                // Skip the base level (already processed) and levels outside the brace span
                                if (level.Id == baseLevel.Id || level.Elevation <= baseLevel.Elevation || level.Elevation > topLevel.Elevation)
                                    continue;

                                string upperLevelAssign = FormatBraceAssign(
                                    lineId,
                                    level.Name,
                                    frameProp.Name,
                                    "PINNED", // Braces typically have pinned connections
                                    24, // Default value for max station spacing
                                    "YES", // Auto mesh
                                    "YES" // Mesh at intersections
                                );

                                sb.AppendLine(upperLevelAssign);
                            }
                        }
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a beam assignment line for E2K format
        /// </summary>
        /// <param name="lineId">ID of the line element</param>
        /// <param name="story">Story or level name</param>
        /// <param name="section">Section or property name</param>
        /// <param name="maxStaSpc">Maximum station spacing</param>
        /// <param name="autoMesh">Auto mesh flag (e.g., "YES" or "NO")</param>
        /// <param name="meshAtIntersections">Mesh at intersections flag (e.g., "YES" or "NO")</param>
        /// <returns>Formatted line assignment for a beam in E2K format</returns>
        private string FormatLineAssign(
            string lineId,
            string story,
            string section,
            double maxStaSpc,
            string autoMesh,
            string meshAtIntersections)
        {
            // Format: LINEASSIGN  "B1"  "Story1"  SECTION "W12X210"  MAXSTASPC 24 AUTOMESH "YES"  MESHATINTERSECTIONS "YES"
            return $"  LINEASSIGN  \"{lineId}\"  \"{story}\"  SECTION \"{section}\"  MAXSTASPC {maxStaSpc} AUTOMESH \"{autoMesh}\"  MESHATINTERSECTIONS \"{meshAtIntersections}\"";
        }

        /// <summary>
        /// Formats a column assignment line for E2K format
        /// </summary>
        /// <param name="lineId">ID of the line element</param>
        /// <param name="story">Story or level name</param>
        /// <param name="section">Section or property name</param>
        /// <param name="release">End release code</param>
        /// <param name="minNumSta">Minimum number of stations</param>
        /// <param name="autoMesh">Auto mesh flag (e.g., "YES" or "NO")</param>
        /// <param name="meshAtIntersections">Mesh at intersections flag (e.g., "YES" or "NO")</param>
        /// <returns>Formatted line assignment for a column in E2K format</returns>
        private string FormatColumnAssign(
            string lineId,
            string story,
            string section,
            string release,
            int minNumSta,
            string autoMesh,
            string meshAtIntersections)
        {
            // Format: LINEASSIGN  "C1"  "Story1"  SECTION "18"  RELEASE "M2J M3J"  MINNUMSTA 3 AUTOMESH "YES"  MESHATINTERSECTIONS "YES"
            return $"  LINEASSIGN  \"{lineId}\"  \"{story}\"  SECTION \"{section}\"  RELEASE \"{release}\"MINNUMSTA {minNumSta} AUTOMESH \"{autoMesh}\"  MESHATINTERSECTIONS \"{meshAtIntersections}\"";
        }

        /// <summary>
        /// Formats a brace assignment line for E2K format
        /// </summary>
        /// <param name="lineId">ID of the line element</param>
        /// <param name="story">Story or level name</param>
        /// <param name="section">Section or property name</param>
        /// <param name="release">End release code</param>
        /// <param name="maxStaSpc">Maximum station spacing</param>
        /// <param name="autoMesh">Auto mesh flag (e.g., "YES" or "NO")</param>
        /// <param name="meshAtIntersections">Mesh at intersections flag (e.g., "YES" or "NO")</param>
        /// <returns>Formatted line assignment for a brace in E2K format</returns>
        private string FormatBraceAssign(
            string lineId,
            string story,
            string section,
            string release,
            double maxStaSpc,
            string autoMesh,
            string meshAtIntersections)
        {
            // Format similar to columns but with different defaults
            return $"  LINEASSIGN  \"{lineId}\"  \"{story}\"  SECTION \"{section}\"  RELEASE \"{release}\"  MAXSTASPC {maxStaSpc} AUTOMESH \"{autoMesh}\"  MESHATINTERSECTIONS \"{meshAtIntersections}\"";
        }
     
    }
}
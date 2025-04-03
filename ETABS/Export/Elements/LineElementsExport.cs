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
    // Exports line elements (beams, columns, braces) for the E2K file format with debugging
    public class LineElementsExport
    {
        // Store line element IDs for mapping between connectivities and assignments
        private readonly Dictionary<string, string> _beamIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _columnIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _braceIdMapping = new Dictionary<string, string>();
        private readonly Dictionary<Point2D, string> _pointMapping;
        private StringBuilder _debugLog = new StringBuilder();

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

            // Clear debug log
            _debugLog.Clear();
            _debugLog.AppendLine("$ DEBUG LINE ELEMENTS LOG");
            _debugLog.AppendLine("$ ======================");

            // First process the connectivities
            string connectivitiesSection = ProcessLineConnectivities(elements);
            sb.AppendLine(connectivitiesSection);
            sb.AppendLine();

            // Then process the assignments using the same mappings
            string assignmentsSection = ProcessLineAssignments(elements, levels, frameProperties);
            sb.AppendLine(assignmentsSection);

            // Add debug log
            sb.AppendLine();
            sb.Append(_debugLog.ToString());

            return sb.ToString();
        }

        // Processes line connectivities for beams, columns, and braces
        private string ProcessLineConnectivities(ElementContainer elements)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Line Connectivities Header
            sb.AppendLine("$ LINE CONNECTIVITIES");

            // Debug log of point mapping
            _debugLog.AppendLine("$ POINT MAPPING STATE");
            foreach (var entry in _pointMapping)
            {
                _debugLog.AppendLine($"$ Point: X={entry.Key.X}, Y={entry.Key.Y}, ID: {entry.Value}");
            }

            // Process Beams
            _debugLog.AppendLine("$ BEAM CONNECTIVITIES");
            if (elements.Beams != null && elements.Beams.Count > 0)
            {
                for (int i = 0; i < elements.Beams.Count; i++)
                {
                    Beam beam = elements.Beams[i];
                    _debugLog.AppendLine($"$ Beam {i + 1} (ID: {beam.Id}):");
                    _debugLog.AppendLine($"$   LevelId: {beam.LevelId}");

                    if (beam.StartPoint != null && beam.EndPoint != null)
                    {
                        // Debug log beam points
                        _debugLog.AppendLine($"$   StartPoint: X={beam.StartPoint.X}, Y={beam.StartPoint.Y}");
                        _debugLog.AppendLine($"$   EndPoint: X={beam.EndPoint.X}, Y={beam.EndPoint.Y}");

                        // Use consistent naming convention (B1, B2, etc.)
                        string beamId = $"B{i + 1}";

                        // Store the mapping for later use in assignments
                        _beamIdMapping[beam.Id] = beamId;

                        // Get point IDs with debug output
                        string startPointId = Utils.GetPointId(beam.StartPoint, _pointMapping);
                        string endPointId = Utils.GetPointId(beam.EndPoint, _pointMapping);

                        _debugLog.AppendLine($"$   Assigned StartPointId: {startPointId}");
                        _debugLog.AppendLine($"$   Assigned EndPointId: {endPointId}");

                        // Debug point lookup
                        _debugLog.AppendLine("$   Point lookup details:");
                        foreach (var entry in _pointMapping)
                        {
                            bool isStartPointMatch = Utils.ArePointsEqual(entry.Key, beam.StartPoint);
                            bool isEndPointMatch = Utils.ArePointsEqual(entry.Key, beam.EndPoint);

                            if (isStartPointMatch || isEndPointMatch)
                            {
                                _debugLog.AppendLine($"$     Comparing with point X={entry.Key.X}, Y={entry.Key.Y}, ID={entry.Value}:");
                                if (isStartPointMatch)
                                    _debugLog.AppendLine($"$       StartPoint MATCH");
                                if (isEndPointMatch)
                                    _debugLog.AppendLine($"$       EndPoint MATCH");
                            }
                        }

                        string beamLine = FormatBeamConnectivity(beam, beamId, startPointId, endPointId);
                        sb.AppendLine(beamLine);
                    }
                    else
                    {
                        _debugLog.AppendLine("$   WARNING: StartPoint or EndPoint is null");
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

        // Formats a beam connectivity for the E2K file with specific point IDs
        private string FormatBeamConnectivity(Beam beam, string beamId, string startPointId, string endPointId)
        {
            // Format: LINE  "B1"  BEAM  "1"  "2"  0
            return $"  LINE  \"{beamId}\"  BEAM  \"{startPointId}\"  \"{endPointId}\"  0";
        }

        // Formats a beam connectivity for the E2K file
        private string FormatBeamConnectivity(Beam beam, string beamId)
        {
            string startPointId = Utils.GetPointId(beam.StartPoint, _pointMapping);
            string endPointId = Utils.GetPointId(beam.EndPoint, _pointMapping);

            // Format: LINE  "B1"  BEAM  "1"  "2"  0
            return $"  LINE  \"{beamId}\"  BEAM  \"{startPointId}\"  \"{endPointId}\"  0";
        }

        // Formats a column connectivity for the E2K file
        private string FormatColumnConnectivity(Column column, string columnId)
        {
            string startPointId = Utils.GetPointId(column.StartPoint, _pointMapping);
            string endPointId = Utils.GetPointId(column.EndPoint, _pointMapping);

            // Format: LINE  "C1"  COLUMN  "9"  "9"  1
            return $"  LINE  \"{columnId}\"  COLUMN  \"{startPointId}\"  \"{endPointId}\"  1";
        }

        // Formats a brace connectivity for the E2K file
        private string FormatBraceConnectivity(Brace brace, string braceId)
        {
            string startPointId = Utils.GetPointId(brace.StartPoint, _pointMapping);
            string endPointId = Utils.GetPointId(brace.EndPoint, _pointMapping);

            // Format: LINE  "BR1"  BRACE  "3"  "4"  0
            return $"  LINE  \"{braceId}\"  BRACE  \"{startPointId}\"  \"{endPointId}\"  0";
        }

        // Processes line assignments for beams, columns, and braces
        private string ProcessLineAssignments(
            ElementContainer elements,
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Line Assignments Header
            sb.AppendLine("$ LINE ASSIGNS");

            // ... rest of the method remains the same ...

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

                    // Add "Story" prefix to level name (except for level 0, which should be "Base")
                    string storyName = beamLevel.Name == "0" ? "Base" : $"Story{beamLevel.Name}";
                    string lineAssign = FormatLineAssign(
                        lineId,
                        storyName,
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

                    // Find top level
                    Level topLevel = null;
                    if (!string.IsNullOrEmpty(column.TopLevelId))
                    {
                        topLevel = Utils.FindLevel(levels, column.TopLevelId);
                    }

                    // If top level not specified or not found, use base level as a fallback
                    Level storyLevel = topLevel != null ? topLevel : baseLevel;

                    // Add "Story" prefix to level name (except for level 0, which should be "Base")
                    string storyName = storyLevel.Name == "0" ? "Base" : $"Story{storyLevel.Name}";

                    // Determine end releases - columns usually have "M2J M3J" at base and "PINNED" above
                    string release = "M2J M3J"; // Base level typically has moment releases at the bottom

                    // Format the column assignment - always using the top story
                    string lineAssign = FormatColumnAssign(
                        lineId,
                        storyName,
                        frameProp.Name,
                        release,
                        3, // Minimum number of stations
                        "YES", // Auto mesh
                        "YES" // Mesh at intersections
                    );

                    sb.AppendLine(lineAssign);
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

                    // Find the top level for this brace
                    Level topLevel = null;
                    if (!string.IsNullOrEmpty(brace.TopLevelId))
                    {
                        topLevel = Utils.FindLevel(levels, brace.TopLevelId);
                    }

                    // If top level not specified or not found, use base level as a fallback
                    Level storyLevel = topLevel != null ? topLevel : baseLevel;

                    // Add "Story" prefix to level name
                    string storyName = $"Story{storyLevel.Name}";

                    // Format the brace assignment - always using the top story
                    string lineAssign = FormatBraceAssign(
                        lineId,
                        storyName,
                        frameProp.Name,
                        "PINNED", // Braces typically have pinned connections
                        24, // Default value for max station spacing
                        "YES", // Auto mesh
                        "YES" // Mesh at intersections
                    );

                    sb.AppendLine(lineAssign);
                }
            }

            return sb.ToString();
        }

        // Formats a beam assignment line for E2K format
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

        // Formats a column assignment line for E2K format
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
            return $"  LINEASSIGN  \"{lineId}\"  \"{story}\"  SECTION \"{section}\"  RELEASE \"{release}\" MINNUMSTA {minNumSta} AUTOMESH \"{autoMesh}\"  MESHATINTERSECTIONS \"{meshAtIntersections}\"";
        }

        // Formats a brace assignment line for E2K format
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
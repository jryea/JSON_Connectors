using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;

namespace ETABS.ToETABS.Elements.Assignment
{
    // Converts column assignment information to ETABS E2K format
    public class ColumnAssignmentToETABS : IAssignmentToETABS
    {
        private List<Column> _columns;
        private IEnumerable<Level> _levels;
        private IEnumerable<FrameProperties> _frameProperties;

        // Sets the data needed for converting column assignments
        public void SetData(
            List<Column> columns,
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties)
        {
            _columns = columns;
            _levels = levels;
            _frameProperties = frameProperties;
        }

        // Converts column assignments to E2K format
        public string ExportAssignments(Dictionary<string, string> idMapping)
        {
            StringBuilder sb = new StringBuilder();

            if (_columns == null || _columns.Count == 0 || idMapping == null || idMapping.Count == 0 || _levels == null)
                return sb.ToString();

            // Group columns by their E2K IDs from the mapping
            var columnGroups = _columns
                .Where(c => idMapping.ContainsKey(c.Id))
                .GroupBy(c => idMapping[c.Id])
                .ToDictionary(g => g.Key, g => g.ToList());

            // Sort levels by elevation (descending)
            var sortedLevels = _levels.OrderByDescending(l => l.Elevation).ToList();

            foreach (var group in columnGroups)
            {
                string lineId = group.Key;
                var columnsInGroup = group.Value;

                // Collect all level IDs referenced by any column in the group
                HashSet<string> relevantLevelIds = new HashSet<string>();
                foreach (var column in columnsInGroup)
                {
                    if (!string.IsNullOrEmpty(column.BaseLevelId))
                        relevantLevelIds.Add(column.BaseLevelId);
                    if (!string.IsNullOrEmpty(column.TopLevelId))
                        relevantLevelIds.Add(column.TopLevelId);
                }

                // Get frame properties from one of the columns
                string sectionName = "Unknown";
                var firstColumn = columnsInGroup.FirstOrDefault();
                if (firstColumn != null)
                {
                    var frameProps = _frameProperties?.FirstOrDefault(fp => fp.Id == firstColumn.FramePropertiesId);
                    if (frameProps != null)
                    {
                        sectionName = frameProps.Name;
                    }
                }

                // For each relevant level
                foreach (var levelId in relevantLevelIds)
                {
                    var level = sortedLevels.FirstOrDefault(l => l.Id == levelId);
                    if (level == null)
                        continue;

                    // Format story name
                    string storyName = level.Name.ToLower() == "base" ? "Base" : $"Story{level.Name}";

                    // Create line assignment
                    sb.AppendLine(FormatColumnAssign(
                        lineId,
                        storyName,
                        sectionName,
                        "FIXED", // Default release
                        3, // Default min num stations
                        "YES", // Default auto mesh
                        "YES"  // Default mesh at intersections
                    ));
                }
            }

            return sb.ToString();
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
            return $"  LINEASSIGN  \"{lineId}\"  \"{story}\"  SECTION \"{section}\"  MINNUMSTA {minNumSta} AUTOMESH \"{autoMesh}\"  MESHATINTERSECTIONS \"{meshAtIntersections}\"";
        }
    }
}
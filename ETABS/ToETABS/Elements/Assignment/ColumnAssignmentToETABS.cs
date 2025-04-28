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

            // Get all levels sorted by elevation (highest to lowest)
            var sortedLevels = _levels.OrderByDescending(l => l.Elevation).ToList();

            foreach (var column in _columns)
            {
                if (!idMapping.TryGetValue(column.Id, out string e2kId))
                    continue;

                // Find base and top levels
                var baseLevel = _levels.FirstOrDefault(l => l.Id == column.BaseLevelId);
                var topLevel = _levels.FirstOrDefault(l => l.Id == column.TopLevelId);

                if (baseLevel == null || topLevel == null)
                    continue;

                // Find section from frame properties
                string sectionName = "Unknown";
                if (!string.IsNullOrEmpty(column.FramePropertiesId))
                {
                    var properties = _frameProperties.FirstOrDefault(p => p.Id == column.FramePropertiesId);
                    if (properties != null)
                        sectionName = properties.Name;
                }

                // Replace Unicode representation of double quote (\u0022) with "inch" in the section name
                sectionName = sectionName.Replace("\u0022", "inch");

                // Create assignments for each story the column spans through
                foreach (var level in sortedLevels)
                {
                    // Skip levels below the base level or above the top level
                    if (level.Elevation < baseLevel.Elevation || level.Elevation > topLevel.Elevation)
                        continue;

                    // Create an assignment for this level
                    string storyName = GetStoryName(level);

                    // Format: LINEASSIGN "C1" "Story1" SECTION "W12X26" MINNUMSTA 3 AUTOMESH "YES" MESHATINTERSECTIONS "YES"
                    sb.AppendLine($"  LINEASSIGN \"{e2kId}\" \"{storyName}\" SECTION \"{sectionName}\" MINNUMSTA 3 AUTOMESH \"YES\" MESHATINTERSECTIONS \"YES\"");

                    Console.WriteLine($"Created column assignment for {e2kId} at story {storyName} with section {sectionName}");
                }
            }

            return sb.ToString();
        }

        // Gets a formatted story name from a level
        private string GetStoryName(Level level)
        {
            // Format story name
            return level.Name.ToLower() == "base" ? "Base" : $"Story{level.Name}";
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;

namespace ETABS.ToETABS.Elements.LineAssignment
{
    /// <summary>
    /// Converts brace assignment information to ETABS E2K format
    /// </summary>
    public class BraceAssignmentToETABS : IAssignmentToETABS
    {
        private List<Brace> _braces;
        private IEnumerable<Level> _levels;
        private IEnumerable<FrameProperties> _frameProperties;

        /// <summary>
        /// Sets the data needed for converting brace assignments
        /// </summary>
        public void SetData(
            List<Brace> braces,
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties)
        {
            _braces = braces;
            _levels = levels;
            _frameProperties = frameProperties;
        }

        /// <summary>
        /// Converts brace assignments to E2K format
        /// </summary>
        public string ExportAssignments(Dictionary<string, string> idMapping)
        {
            StringBuilder sb = new StringBuilder();

            if (_braces == null || _braces.Count == 0 || idMapping == null || idMapping.Count == 0)
                return sb.ToString();

            foreach (var brace in _braces)
            {
                // Check if we have a mapping for this brace ID
                if (!idMapping.TryGetValue(brace.Id, out string lineId))
                    continue;

                // Get the brace properties
                string sectionName = "Unknown";
                var frameProps = _frameProperties?.FirstOrDefault(fp => fp.Id == brace.FramePropertiesId);
                if (frameProps != null)
                {
                    sectionName = frameProps.Name;
                }

                // Find the top level for this brace
                var level = _levels?.FirstOrDefault(l => l.Id == brace.TopLevelId);

                string storyName = "Story1"; // Default
                if (level != null)
                {
                    // Format story name
                    storyName = level.Name.ToLower() == "base" ? "Base" : $"Story{level.Name}";
                }

                // Create line assignment with appropriate release (typically "PINNED")
                sb.AppendLine(FormatBraceAssign(lineId, storyName, sectionName));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a brace assignment line for E2K format
        /// </summary>
        private string FormatBraceAssign(
            string lineId,
            string story,
            string section,
            string release = "PINNED",
            double maxStaSpc = 24,
            string autoMesh = "YES",
            string meshAtIntersections = "YES")
        {
            return $"  LINEASSIGN  \"{lineId}\"  \"{story}\"  SECTION \"{section}\"  RELEASE \"{release}\"  MAXSTASPC {maxStaSpc} AUTOMESH \"{autoMesh}\"  MESHATINTERSECTIONS \"{meshAtIntersections}\"";
        }
    }
}

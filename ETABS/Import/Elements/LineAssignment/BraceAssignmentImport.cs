using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;

namespace ETABS.Import.Elements.LineAssignment
{
    // Converts brace assignment information to ETABS E2K format
    public class BraceAssignmentImport : IAssignmentImport
    {
        private List<Brace> _braces;
        private IEnumerable<Level> _levels;
        private IEnumerable<FrameProperties> _frameProperties;

        // Sets the data needed for converting brace assignments
        public void SetData(
            List<Brace> braces,
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties)
        {
            _braces = braces;
            _levels = levels;
            _frameProperties = frameProperties;
        }

        // Converts brace assignments to E2K format
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
                sb.AppendLine(FormatBraceAssign(lineId, storyName, sectionName, brace));
            }

            return sb.ToString();
        }

        // Formats a brace assignment line for E2K format
        private string FormatBraceAssign(
            string lineId,
            string story,
            string section,
            Brace brace,
            string release = "PINNED",
            double maxStaSpc = 24,
            string autoMesh = "YES",
            string meshAtIntersections = "YES")
        {
            StringBuilder sb = new StringBuilder($"  LINEASSIGN  \"{lineId}\"  \"{story}\"  SECTION \"{section}\"  RELEASE \"{release}\"");

            // Add modifiers if they deviate from default value of 1.0
            if (brace?.ETABSModifiers != null)
            {
                // Using Math.Abs to compare floating point values with a small tolerance
                if (Math.Abs(brace.ETABSModifiers.Area - 1.0) > 0.0001)
                    sb.Append($" PROPMODA {brace.ETABSModifiers.Area:0.####}");
                if (Math.Abs(brace.ETABSModifiers.A22 - 1.0) > 0.0001)
                    sb.Append($" PROPMODA2 {brace.ETABSModifiers.A22:0.####}");
                if (Math.Abs(brace.ETABSModifiers.A33 - 1.0) > 0.0001)
                    sb.Append($" PROPMODA3 {brace.ETABSModifiers.A33:0.####}");
                if (Math.Abs(brace.ETABSModifiers.Torsion - 1.0) > 0.0001)
                    sb.Append($" PROPMODT {brace.ETABSModifiers.Torsion:0.####}");
                if (Math.Abs(brace.ETABSModifiers.I22 - 1.0) > 0.0001)
                    sb.Append($" PROPMODI22 {brace.ETABSModifiers.I22:0.####}");
                if (Math.Abs(brace.ETABSModifiers.I33 - 1.0) > 0.0001)
                    sb.Append($" PROPMODI33 {brace.ETABSModifiers.I33:0.####}");
                if (Math.Abs(brace.ETABSModifiers.Mass - 1.0) > 0.0001)
                    sb.Append($" PROPMODM {brace.ETABSModifiers.Mass:0.####}");
                if (Math.Abs(brace.ETABSModifiers.Weight - 1.0) > 0.0001)
                    sb.Append($" PROPMODW {brace.ETABSModifiers.Weight:0.####}");
            }

            sb.Append($" MAXSTASPC {maxStaSpc} AUTOMESH \"{autoMesh}\"  MESHATINTERSECTIONS \"{meshAtIntersections}\"");

            return sb.ToString();
        }
    }
}
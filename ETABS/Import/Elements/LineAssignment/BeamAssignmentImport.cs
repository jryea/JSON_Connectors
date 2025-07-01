using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;

namespace ETABS.Import.Elements.LineAssignment
    {
        // Converts beam assignment information to ETABS E2K format
        public class BeamAssignmentImport : IAssignmentImport
        {
            private List<Beam> _beams;
            private IEnumerable<Level> _levels;
            private IEnumerable<FrameProperties> _frameProperties;

            // Sets the data needed for converting beam assignments
            public void SetData(
                List<Beam> beams,
                IEnumerable<Level> levels,
                IEnumerable<FrameProperties> frameProperties)
            {
                _beams = beams;
                _levels = levels;
                _frameProperties = frameProperties;
            }

            // Converts beam assignments to E2K format
            public string ExportAssignments(Dictionary<string, string> idMapping)
            {
                StringBuilder sb = new StringBuilder();

                if (_beams == null || _beams.Count == 0 || idMapping == null || idMapping.Count == 0)
                    return sb.ToString();

                foreach (var beam in _beams)
                {
                    // Check if we have a mapping for this beam ID
                    if (!idMapping.TryGetValue(beam.Id, out string lineId))
                        continue;

                    // Get the beam properties
                    string sectionName = "Unknown";
                    var frameProps = _frameProperties?.FirstOrDefault(fp => fp.Id == beam.FramePropertiesId);
                    if (frameProps != null)
                    {
                        sectionName = frameProps.Name;
                    }

                    // Find the level for this beam
                    var level = _levels?.FirstOrDefault(l => l.Id == beam.LevelId);
                    string storyName = "Story1"; // Default
                    if (level != null)
                    {
                        // Format story name (add "Story" prefix except for "Base")
                        storyName = level.Name.ToLower() == "base" ? "Base" : level.Name;
                    }

                    // Create line assignment based on beam type (regular or joist)
                    if (beam.IsJoist)
                    {
                        sb.AppendLine(FormatJoistAssign(lineId, storyName, sectionName, beam));
                    }
                    else
                    {
                        sb.AppendLine(FormatBeamAssign(lineId, storyName, sectionName, beam));
                    }
                }

                return sb.ToString();
            }

            // Formats a beam assignment line for E2K format
            private string FormatBeamAssign(
                string lineId,
                string story,
                string section,
                Beam beam,
                string cardinalPoint = "8",
                double maxStaSpc = 24,
                string autoMesh = "YES",
                string meshAtIntersections = "YES")
            {
                StringBuilder sb = new StringBuilder($"  LINEASSIGN  \"{lineId}\"  \"{story}\"  SECTION \"{section}\"  CARDINALPT {cardinalPoint}");

                // Add modifiers if they deviate from default value of 1.0
                if (beam?.FrameModifiers != null)
                {
                    if (Math.Abs(beam.FrameModifiers.Area - 1.0) > 0.0001)
                        sb.Append($" PROPMODA {beam.FrameModifiers.Area}");
                    if (Math.Abs(beam.FrameModifiers.A22 - 1.0) > 0.0001)
                        sb.Append($" PROPMODA2 {beam.FrameModifiers.A22}");
                    if (Math.Abs(beam.FrameModifiers.A33 - 1.0) > 0.0001)
                        sb.Append($" PROPMODA3 {beam.FrameModifiers.A33}");
                    if (Math.Abs(beam.FrameModifiers.Torsion - 1.0) > 0.0001)
                        sb.Append($" PROPMODT {beam.FrameModifiers.Torsion}");
                    if (Math.Abs(beam.FrameModifiers.I22 - 1.0) > 0.0001)
                        sb.Append($" PROPMODI22 {beam.FrameModifiers.I22}");
                    if (Math.Abs(beam.FrameModifiers.I33 - 1.0) > 0.0001)
                        sb.Append($" PROPMODI33 {beam.FrameModifiers.I33}");
                    if (Math.Abs(beam.FrameModifiers.Mass - 1.0) > 0.0001)
                        sb.Append($" PROPMODM {beam.FrameModifiers.Mass}");
                    if (Math.Abs(beam.FrameModifiers.Weight - 1.0) > 0.0001)
                        sb.Append($" PROPMODW {beam.FrameModifiers.Weight}");
                }

                sb.Append($"  MAXSTASPC {maxStaSpc} AUTOMESH \"{autoMesh}\"  MESHATINTERSECTIONS \"{meshAtIntersections}\"");
                return sb.ToString();
            }

            // Formats a joist assignment line for E2K format
            private string FormatJoistAssign(
                string lineId,
                string story,
                string section,
                Beam beam,
                string cardinalPoint = "8",
                double maxStaSpc = 24,
                string autoMesh = "YES",
                string meshAtIntersections = "YES")
            {
                StringBuilder sb = new StringBuilder($"  LINEASSIGN  \"{lineId}\"  \"{story}\"  SECTION \"{section}\"  RELEASE \"TI M2I M2J M3I M3J\" CARDINALPT {cardinalPoint}");

                // Add modifiers if they deviate from default value of 1.0
                if (beam?.FrameModifiers != null)
                {
                    if (Math.Abs(beam.FrameModifiers.Area - 1.0) > 0.0001)
                        sb.Append($" PROPMODA {beam.FrameModifiers.Area}");
                    if (Math.Abs(beam.FrameModifiers.A22 - 1.0) > 0.0001)
                        sb.Append($" PROPMODA2 {beam.FrameModifiers.A22}");
                    if (Math.Abs(beam.FrameModifiers.A33 - 1.0) > 0.0001)
                        sb.Append($" PROPMODA3 {beam.FrameModifiers.A33}");
                    if (Math.Abs(beam.FrameModifiers.Torsion - 1.0) > 0.0001)
                        sb.Append($" PROPMODT {beam.FrameModifiers.Torsion}");
                    if (Math.Abs(beam.FrameModifiers.I22 - 1.0) > 0.0001)
                        sb.Append($" PROPMODI22 {beam.FrameModifiers.I22}");
                    if (Math.Abs(beam.FrameModifiers.I33 - 1.0) > 0.0001)
                        sb.Append($" PROPMODI33 {beam.FrameModifiers.I33}");
                    if (Math.Abs(beam.FrameModifiers.Mass - 1.0) > 0.0001)
                        sb.Append($" PROPMODM {beam.FrameModifiers.Mass}");
                    if (Math.Abs(beam.FrameModifiers.Weight - 1.0) > 0.0001)
                        sb.Append($" PROPMODW {beam.FrameModifiers.Weight}");
                }

                sb.Append($"  MAXSTASPC {maxStaSpc} AUTOMESH \"{autoMesh}\"  MESHATINTERSECTIONS \"{meshAtIntersections}\"");
                return sb.ToString();
            }
        }
    }
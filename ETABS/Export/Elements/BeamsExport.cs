using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;

namespace ETABS.Export.Elements
{
    /// <summary>
    /// Converts Core Beam objects to ETABS E2K format text
    /// </summary>
    public class BeamsExport
    {
        /// <summary>
        /// Converts a collection of Beam objects to E2K format text
        /// </summary>
        /// <param name="beams">Collection of Beam objects</param>
        /// <param name="levels">Collection of Level objects for reference</param>
        /// <returns>E2K format text for beams</returns>
        public string ConvertToE2K(List<Beam> beams, List<Level> levels)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Beam Section Header
            sb.AppendLine("$ FRAME OBJECTS - BEAMS");

            int beamCounter = 1;
            foreach (var beam in beams)
            {
                // Get level info
                var level = levels.Find(l => l.Id == beam.LevelId);
                string levelName = level?.Name ?? "Unknown";

                // Format line format for E2K:
                // LINE "B1" BEAM "x1" "x2" 0
                string beamId = $"B{beamCounter++}";
                sb.AppendLine($"LINE \"{beamId}\" BEAM \"{beam.StartPoint.X} {beam.StartPoint.Y}\" " +
                              $"\"{beam.EndPoint.X} {beam.EndPoint.Y}\" 0");

                // Add beam assignment
                sb.AppendLine($"LINEASSIGN \"{beamId}\" \"{levelName}\" SECTION \"{beam.FramePropertiesId}\" " +
                              $"MAXSTASPC 24 AUTOMESH \"YES\" MESHATINTERSECTIONS \"YES\"");
            }

            return sb.ToString();
        }
    }
}
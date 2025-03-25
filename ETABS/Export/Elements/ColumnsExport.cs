using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;

namespace ETABS.Export.Elements
{
    /// <summary>
    /// Converts Core Column objects to ETABS E2K format text
    /// </summary>
    public class ColumnsExport
    {
        /// <summary>
        /// Converts a collection of Column objects to E2K format text
        /// </summary>
        /// <param name="columns">Collection of Column objects</param>
        /// <param name="levels">Collection of Level objects for reference</param>
        /// <returns>E2K format text for columns</returns>
        public string ConvertToE2K(List<Column> columns, List<Level> levels)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Column Section Header
            sb.AppendLine("$ FRAME OBJECTS - COLUMNS");

            int columnCounter = 1;
            foreach (var column in columns)
            {
                // Get level info
                var baseLevel = levels.Find(l => l.Id == column.BaseLevelId);
                var topLevel = levels.Find(l => l.Id == column.TopLevelId);

                string baseLevelName = baseLevel?.Name ?? "Unknown";
                string topLevelName = topLevel?.Name ?? "Unknown";

                // Format line format for E2K:
                // LINE "C1" COLUMN "x y" "x y" 1
                string columnId = $"C{columnCounter++}";
                sb.AppendLine($"LINE \"{columnId}\" COLUMN \"{column.StartPoint.X} {column.StartPoint.Y}\" " +
                              $"\"{column.EndPoint.X} {column.EndPoint.Y}\" 1");

                // Add column assignment for each story between base and top level
                int baseIndex = levels.IndexOf(baseLevel);
                int topIndex = levels.IndexOf(topLevel);

                if (baseIndex >= 0 && topIndex >= 0)
                {
                    for (int i = baseIndex; i <= topIndex; i++)
                    {
                        string levelName = levels[i].Name;
                        string pinned = i == baseIndex ? "M2J M3J" : "PINNED";

                        sb.AppendLine($"LINEASSIGN \"{columnId}\" \"{levelName}\" SECTION \"{column.FramePropertiesId}\" " +
                                      $"RELEASE \"{pinned}\" MINNUMSTA 3 AUTOMESH \"YES\" MESHATINTERSECTIONS \"YES\"");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
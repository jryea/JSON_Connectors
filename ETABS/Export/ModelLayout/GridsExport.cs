using System.Collections.Generic;
using System.Text;
using Core.Models.ModelLayout;

namespace ETABS.Export.ModelLayout
{
    /// <summary>
    /// Converts Core Grid objects to ETABS E2K format text
    /// </summary>
    public class GridsExport
    {
        /// <summary>
        /// Converts a collection of Grid objects to E2K format text
        /// </summary>
        /// <param name="grids">Collection of Grid objects</param>
        /// <returns>E2K format text for grids</returns>
        public string ConvertToE2K(List<Grid> grids)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Grid System Header
            sb.AppendLine("$ GRIDS");
            sb.AppendLine("GRIDSYSTEM \"G1\"  TYPE \"CARTESIAN\"  BUBBLESIZE 60");

            foreach (var grid in grids)
            {
                // Determine direction (X or Y) based on coordinates
                bool isXDirection = grid.StartPoint.Y.Equals(grid.EndPoint.Y);
                string direction = isXDirection ? "X" : "Y";

                // Determine coordinate value
                double coordinate = isXDirection ? grid.StartPoint.X : grid.StartPoint.Y;

                // Determine bubble location
                string bubbleLoc = "End";
                if (grid.StartPoint.IsBubble && !grid.EndPoint.IsBubble)
                {
                    bubbleLoc = "Start";
                }
                else if (grid.StartPoint.IsBubble && grid.EndPoint.IsBubble)
                {
                    bubbleLoc = "Both";
                }

                // Format: GRID "G1"  LABEL "A"  DIR "X"  COORD 0 VISIBLE "Yes"  BUBBLELOC "End"
                sb.AppendLine($"GRID \"G1\"  LABEL \"{grid.Name}\"  DIR \"{direction}\"  " +
                             $"COORD {coordinate} VISIBLE \"Yes\"  BUBBLELOC \"{bubbleLoc}\"");
            }

            return sb.ToString();
        }
    }
}
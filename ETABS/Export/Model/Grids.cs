using System.Collections.Generic;
using System.Text;
using Core.Models.Model;

namespace ETABS.Export.Model
{
    /// <summary>
    /// Converts Core Grid objects to ETABS E2K format text
    /// </summary>
    public class Grids
    {
        /// <summary>
        /// Converts a collection of Grid objects to E2K format text
        /// </summary>
        /// <param name="grids">Collection of Grid objects</param>
        /// <returns>E2K format text for grids</returns>
        public string ConvertToE2K(IEnumerable<Grid> grids)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Grid Section Header
            sb.AppendLine("$ GRID LINES SYSTEM");
            sb.AppendLine("$ GRID LINE DEFINITIONS");

            foreach (var grid in grids)
            {
                // Format for grid line in E2K:
                // GRID-LINE "NAME" [BUBBLE=Yes/No] [COLOR=RGB(r,g,b)] [VISIBLE=Yes/No] 
                // {"coordinate system"} {"grid line type"} {"grid line coordinate 1"} {"grid line coordinate 2"} {"grid line coordinate 3"} 

                string gridLine = FormatGridLine(grid);
                sb.AppendLine(gridLine);
            }

            return sb.ToString();
        }

        private string FormatGridLine(Grid grid)
        {
            // Determine if grid is X or Y direction based on coordinates
            bool isXDirection = grid.StartPoint.Y.Equals(grid.EndPoint.Y);
            string gridType = isXDirection ? "X" : "Y";

            // Format: GRID-LINE "A" BUBBLE=No COLOR=RGB(000,000,000) VISIBLE=Yes COORDINATES CARTESIAN "Global" "Y" 0 0 0
            string bubbleStatus = grid.StartPoint.IsBubble || grid.EndPoint.IsBubble ? "Yes" : "No";

            // Convert inches to feet for ETABS (if your Core model is in inches)
            double x1 = grid.StartPoint.X / 12.0;
            double y1 = grid.StartPoint.Y / 12.0;
            double z1 = grid.StartPoint.Z / 12.0;

            double x2 = grid.EndPoint.X / 12.0;
            double y2 = grid.EndPoint.Y / 12.0;
            double z2 = grid.EndPoint.Z / 12.0;

            // Determine appropriate coordinates based on grid direction
            double coord = isXDirection ? x1 : y1;

            return $"GRID-LINE \"{grid.Name}\" BUBBLE={bubbleStatus} COLOR=RGB(000,000,000) VISIBLE=Yes " +
                   $"COORDINATES CARTESIAN \"Global\" \"{gridType}\" {coord:F6} {0:F6} {0:F6}";
        }
    }
}
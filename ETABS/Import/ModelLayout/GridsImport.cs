using System;
using System.Collections.Generic;
using System.Text;
using Core.Models.ModelLayout;
using Utils = Core.Utilities.ModelUtils;

namespace ETABS.Import.ModelLayout
{
    // Converts Core Grid objects to ETABS E2K format text
    public class GridsImport
    {
        // Converts a collection of Grid objects to E2K format text
        public string ConvertToE2K(List<Grid> grids)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Grid System Header
            sb.AppendLine("$ GRIDS");
            sb.AppendLine("\tGRIDSYSTEM \"G1\"  TYPE \"CARTESIAN\"  BUBBLESIZE 60");

            foreach (var grid in grids)
            {
                // Check if the grid is vertical or horizontal (orthogonal)
                bool isVertical = Math.Abs(grid.EndPoint.X - grid.StartPoint.X) < 0.001;
                bool isHorizontal = Math.Abs(grid.EndPoint.Y - grid.StartPoint.Y) < 0.001;

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

                if (isVertical || isHorizontal)
                {
                    // Handle orthogonal grids (exactly vertical or horizontal)
                    bool isYDirection = Utils.AreLinePointsVertical(grid.StartPoint, grid.EndPoint);
                    string direction = isYDirection ? "X" : "Y";
                    double coordinate = isYDirection ? grid.StartPoint.X : grid.StartPoint.Y;

                    // Round coordinate to the nearest 0.25 inches
                    coordinate = Math.Round(coordinate * 4) / 4;

                    // Format: GRID "G1" LABEL "A" DIR "X" COORD 0 VISIBLE "Yes" BUBBLELOC "End"
                    sb.AppendLine($"\tGRID \"G1\"  LABEL \"{grid.Name}\"  DIR \"{direction}\"  " +
                                 $"COORD {coordinate} VISIBLE \"Yes\"  BUBBLELOC \"{bubbleLoc}\"");
                }
                else
                {
                    // Handle angled grids
                    // Round coordinates to the nearest 0.25 inches
                    double startX = Math.Round(grid.StartPoint.X * 4) / 4;
                    double startY = Math.Round(grid.StartPoint.Y * 4) / 4;
                    double endX = Math.Round(grid.EndPoint.X * 4) / 4;
                    double endY = Math.Round(grid.EndPoint.Y * 4) / 4;

                    // Format: GENGRID "G1" LABEL "AngledGrid" X1 0 Y1 0 X2 720 Y2 720 VISIBLE "Yes" BUBBLELOC "Both"
                    sb.AppendLine($"\tGENGRID \"G1\"  LABEL \"{grid.Name}\"  " +
                                 $"X1 {startX} Y1 {startY} " +
                                 $"X2 {endX} Y2 {endY} " +
                                 $"VISIBLE \"Yes\"  BUBBLELOC \"{bubbleLoc}\"");
                }
            }

            return sb.ToString();
        }
    }
}

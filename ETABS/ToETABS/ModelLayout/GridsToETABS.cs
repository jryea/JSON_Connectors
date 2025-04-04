﻿using System.Collections.Generic;
using System.Text;
using Core.Models.ModelLayout;
using Utils = Core.Utilities.Utilities;

namespace ETABS.Export.ModelLayout
{

    // Converts Core Grid objects to ETABS E2K format text
    public class GridsToETABS
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
                // Determine direction (X or Y) based on coordinates
                bool isYDirection = Utils.AreLinePointsVertical(grid.StartPoint, grid.EndPoint);
                string direction = isYDirection ? "X" : "Y";

                // Determine coordinate value
                double coordinate = isYDirection ? grid.StartPoint.X : grid.StartPoint.Y;

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
                sb.AppendLine($"\tGRID \"G1\"  LABEL \"{grid.Name}\"  DIR \"{direction}\"  " +
                             $"COORD {coordinate} VISIBLE \"Yes\"  BUBBLELOC \"{bubbleLoc}\"");
            }

            return sb.ToString();
        }
    }
}
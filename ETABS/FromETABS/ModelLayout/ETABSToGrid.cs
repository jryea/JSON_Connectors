using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Utilities;
using ETABS.Import.Utilities;

namespace ETABS.Import.ModelLayout
{
    // Imports grid definitions from ETABS E2K file
    public class ETABSToGrid
    {
        private readonly ETABSToPoints _pointsCollector;

        // Initializes a new instance of GridsImport
        public ETABSToGrid(ETABSToPoints pointsCollector = null)
        {
            _pointsCollector = pointsCollector ?? new ETABSToPoints();
        }

        // Imports grids from E2K GRIDS section
        public List<Grid> Import(string gridsSection)
        {
            var grids = new List<Grid>();

            if (string.IsNullOrWhiteSpace(gridsSection))
                return grids;

            // Extract grid system definition
            // Format: GRIDSYSTEM "G1" TYPE "CARTESIAN" BUBBLESIZE 60
            var gridSystemPattern = new Regex(@"^\s*GRIDSYSTEM\s+""([^""]+)""\s+TYPE\s+""([^""]+)""\s+BUBBLESIZE\s+(\d+)",
                RegexOptions.Multiline);

            var gridSystemMatch = gridSystemPattern.Match(gridsSection);
            string gridSystemName = "G1"; // Default grid system name
            double bubbleSize = 60; // Default bubble size

            if (gridSystemMatch.Success && gridSystemMatch.Groups.Count >= 4)
            {
                gridSystemName = gridSystemMatch.Groups[1].Value;
                bubbleSize = Convert.ToDouble(gridSystemMatch.Groups[3].Value);
            }

            // Extract grid definitions
            // Format: GRID "G1" LABEL "A" DIR "X" COORD 0 VISIBLE "Yes" BUBBLELOC "End"
            var gridPattern = new Regex(@"^\s*GRID\s+""([^""]+)""\s+LABEL\s+""([^""]+)""\s+DIR\s+""([^""]+)""\s+COORD\s+([\d\.\-]+)\s+VISIBLE\s+""([^""]+)""\s+BUBBLELOC\s+""([^""]+)""",
                RegexOptions.Multiline);

            var gridMatches = gridPattern.Matches(gridsSection);

            foreach (Match match in gridMatches)
            {
                if (match.Groups.Count >= 7)
                {
                    string systemName = match.Groups[1].Value;
                    string label = match.Groups[2].Value;
                    string direction = match.Groups[3].Value;
                    double coordinate = Convert.ToDouble(match.Groups[4].Value);
                    string bubbleLocStr = match.Groups[6].Value;

                    // Skip if not part of the main grid system
                    if (systemName != gridSystemName) continue;

                    // Create grid based on direction and coordinate
                    var grid = CreateGrid(label, direction, coordinate, bubbleLocStr);
                    if (grid != null)
                    {
                        grids.Add(grid);
                    }
                }
            }

            return grids;
        }

        // Creates a Grid object based on direction and coordinate
        private Grid CreateGrid(string name, string direction, double coordinate, string bubbleLocStr)
        {
            // Determine bubble location flags
            bool startBubble = false;
            bool endBubble = false;

            switch (bubbleLocStr.ToLower())
            {
                case "start":
                    startBubble = true;
                    break;
                case "end":
                    endBubble = true;
                    break;
                case "both":
                    startBubble = true;
                    endBubble = true;
                    break;
            }

            // Create grid points based on direction
            GridPoint startPoint, endPoint;

            if (direction.ToUpper() == "X")
            {
                // X direction grid is vertical (constant X coordinate)
                startPoint = new GridPoint(coordinate, -1000, 0, startBubble);
                endPoint = new GridPoint(coordinate, 1000, 0, endBubble);
            }
            else if (direction.ToUpper() == "Y")
            {
                // Y direction grid is horizontal (constant Y coordinate)
                startPoint = new GridPoint(-1000, coordinate, 0, startBubble);
                endPoint = new GridPoint(1000, coordinate, 0, endBubble);
            }
            else
            {
                // Unsupported direction
                return null;
            }

            // Create the grid
            return new Grid
            {
                Id = IdGenerator.Generate(IdGenerator.Layout.GRID),
                Name = name,
                StartPoint = startPoint,
                EndPoint = endPoint
            };
        }

        // Sets custom start and end points for grids based on model bounds
        public void AdjustGridExtents(List<Grid> grids, double minX, double maxX, double minY, double maxY)
        {
            // Add padding to model bounds
            const double padding = 100;
            minX -= padding;
            maxX += padding;
            minY -= padding;
            maxY += padding;

            foreach (var grid in grids)
            {
                if (grid.StartPoint == null || grid.EndPoint == null) continue;

                bool isVertical = Math.Abs(grid.StartPoint.X - grid.EndPoint.X) < 0.001;
                if (isVertical)
                {
                    // Vertical grid - adjust Y coordinates
                    double x = grid.StartPoint.X;
                    bool startBubble = grid.StartPoint.IsBubble;
                    bool endBubble = grid.EndPoint.IsBubble;

                    grid.StartPoint = new GridPoint(x, minY, 0, startBubble);
                    grid.EndPoint = new GridPoint(x, maxY, 0, endBubble);
                }
                else
                {
                    // Horizontal grid - adjust X coordinates
                    double y = grid.StartPoint.Y;
                    bool startBubble = grid.StartPoint.IsBubble;
                    bool endBubble = grid.EndPoint.IsBubble;

                    grid.StartPoint = new GridPoint(minX, y, 0, startBubble);
                    grid.EndPoint = new GridPoint(maxX, y, 0, endBubble);
                }
            }
        }
    }
}
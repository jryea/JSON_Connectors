using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Models.Elements;
using Core.Models.Model;

namespace ETABS.Core.Import.Model
{
    /// <summary>
    /// Extracts Grid objects from ETABS E2K format text
    /// </summary>
    public class GridImporter
    {
        /// <summary>
        /// Extracts grid definitions from E2K file lines
        /// </summary>
        /// <param name="e2kLines">Array of text lines from an E2K file</param>
        /// <returns>Collection of Grid objects</returns>
        public IEnumerable<Grid> ExtractFromE2K(string[] e2kLines)
        {
            var grids = new List<Grid>();

            // Find grid lines in the E2K file
            // Expected format: GRID "GLOBAL" "A" "X" 0 BUBBLE=No COLOR=RGB(0,0,0) VISIBLE=Yes
            var gridLinePattern = new Regex(@"^\s*GRID\s+""([^""]+)""\s+""([^""]+)""\s+""([^""]+)""\s+([\d.-]+)(?:\s+BUBBLE=(\w+))?", RegexOptions.IgnoreCase);

            foreach (var line in e2kLines)
            {
                var match = gridLinePattern.Match(line);
                if (match.Success)
                {
                    try
                    {
                        // Extract grid data from the regex match
                        string gridSystem = match.Groups[1].Value; // Usually "GLOBAL"
                        string gridName = match.Groups[2].Value;   // Grid label like "A", "1", etc.
                        string gridType = match.Groups[3].Value;   // "X" or "Y"
                        double coordinate = Convert.ToDouble(match.Groups[4].Value); // Grid line coordinate
                        bool hasBubble = match.Groups.Count > 5 &&
                                         match.Groups[5].Success &&
                                         match.Groups[5].Value.Equals("Yes", StringComparison.OrdinalIgnoreCase);

                        // Create grid points based on grid type
                        // Convert from feet to inches (assuming E2K uses feet)
                        coordinate *= 12.0; // Convert to inches

                        GridPoint startPoint, endPoint;

                        if (gridType.Equals("X", StringComparison.OrdinalIgnoreCase))
                        {
                            // X-type grid is a vertical line with constant X
                            startPoint = new GridPoint(coordinate, -1000, 0, hasBubble);
                            endPoint = new GridPoint(coordinate, 1000, 0, hasBubble);
                        }
                        else
                        {
                            // Y-type grid is a horizontal line with constant Y
                            startPoint = new GridPoint(-1000, coordinate, 0, hasBubble);
                            endPoint = new GridPoint(1000, coordinate, 0, hasBubble);
                        }

                        // Create and add the grid
                        var grid = new Grid(gridName, startPoint, endPoint);
                        grids.Add(grid);
                    }
                    catch (Exception ex)
                    {
                        // Log error or handle invalid grid format
                        Console.WriteLine($"Error parsing grid line: {line}. {ex.Message}");
                    }
                }
            }

            return grids;
        }
    }
}
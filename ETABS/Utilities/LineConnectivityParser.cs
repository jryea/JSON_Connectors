using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ETABS.Import.Utilities
{
    /// <summary>
    /// Utility class to parse line connectivities from E2K files
    /// </summary>
    public class LineConnectivityParser
    {
        // Dictionaries to store the parsed line elements by their ID
        public Dictionary<string, LineConnectivity> Beams { get; private set; } = new Dictionary<string, LineConnectivity>();
        public Dictionary<string, LineConnectivity> Columns { get; private set; } = new Dictionary<string, LineConnectivity>();
        public Dictionary<string, LineConnectivity> Braces { get; private set; } = new Dictionary<string, LineConnectivity>();

        /// <summary>
        /// Line connectivity information
        /// </summary>
        public class LineConnectivity
        {
            public string LineId { get; set; }
            public string Type { get; set; }
            public string Point1Id { get; set; }
            public string Point2Id { get; set; }
            public int Angle { get; set; }
        }

        /// <summary>
        /// Parses the LINE CONNECTIVITIES section from E2K content
        /// </summary>
        /// <param name="lineConnectivitiesSection">The LINE CONNECTIVITIES section content from E2K file</param>
        public void ParseLineConnectivities(string lineConnectivitiesSection)
        {
            if (string.IsNullOrWhiteSpace(lineConnectivitiesSection))
                return;

            // Regular expression to match line connectivity lines
            // Format: LINE "B1" BEAM "9" "10" 0
            var linePattern = new Regex(@"^\s*LINE\s+""([^""]+)""\s+(BEAM|COLUMN|BRACE)\s+""([^""]+)""\s+""([^""]+)""\s+([\d\-]+)",
                RegexOptions.Multiline);

            var matches = linePattern.Matches(lineConnectivitiesSection);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 6)
                {
                    var connectivity = new LineConnectivity
                    {
                        LineId = match.Groups[1].Value,
                        Type = match.Groups[2].Value,
                        Point1Id = match.Groups[3].Value,
                        Point2Id = match.Groups[4].Value,
                        Angle = Convert.ToInt32(match.Groups[5].Value)
                    };

                    // Add to the appropriate dictionary based on element type
                    switch (connectivity.Type.ToUpper())
                    {
                        case "BEAM":
                            Beams[connectivity.LineId] = connectivity;
                            break;
                        case "COLUMN":
                            Columns[connectivity.LineId] = connectivity;
                            break;
                        case "BRACE":
                            Braces[connectivity.LineId] = connectivity;
                            break;
                    }
                }
            }
        }
    }
}
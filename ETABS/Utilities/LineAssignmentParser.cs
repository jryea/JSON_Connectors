using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ETABS.Utilities
{
    // Parses line assignment data from ETABS E2K file
    public class LineAssignmentParser
    {
        // Dictionary to store assignments by line ID
        private Dictionary<string, List<LineAssignment>> _lineAssignments = new Dictionary<string, List<LineAssignment>>();

        // Public accessor for line assignments
        public Dictionary<string, List<LineAssignment>> LineAssignments => _lineAssignments;

        // Parses line assignments from LINE ASSIGNS section
        public void ParseLineAssignments(string lineAssignsSection)
        {
            if (string.IsNullOrWhiteSpace(lineAssignsSection))
                return;

            LineAssignments.Clear();

            // Regular expression to match line assignment lines - making the RELEASE pattern more robust
            var basicPattern = new Regex(@"^\s*LINEASSIGN\s+""([^""]+)""\s+""([^""]+)""\s+SECTION\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Updated pattern for releases - make it more flexible
            var releasePattern = new Regex(@"RELEASE\s+""([^""]+)""", RegexOptions.Singleline);

            // Pattern for lateral flag
            var lateralPattern = new Regex(@"ISLATERAL\s+""([^""]+)""", RegexOptions.Singleline);

            var matches = basicPattern.Matches(lineAssignsSection);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    string lineId = match.Groups[1].Value;
                    string story = match.Groups[2].Value;
                    string section = match.Groups[3].Value;

                    string fullLine = match.Value;

                    // Get the full line from the original section to ensure we capture everything
                    int lineStart = match.Index;
                    int lineEnd = lineAssignsSection.IndexOf('\n', lineStart);
                    if (lineEnd == -1) lineEnd = lineAssignsSection.Length;
                    string completeLine = lineAssignsSection.Substring(lineStart, lineEnd - lineStart);

                    // Check for release conditions - using the complete line
                    string release = "";
                    Match releaseMatch = releasePattern.Match(completeLine);
                    if (releaseMatch.Success)
                    {
                        release = releaseMatch.Groups[1].Value;
                    }

                    // Check for lateral flag
                    bool isLateral = false;
                    Match lateralMatch = lateralPattern.Match(completeLine);
                    if (lateralMatch.Success)
                    {
                        isLateral = lateralMatch.Groups[1].Value.ToUpper() == "YES";
                    }

                    // Create assignment object
                    var assignment = new LineAssignment
                    {
                        LineId = lineId,
                        Story = story,
                        Section = section,
                        ReleaseCondition = release,
                        IsLateral = isLateral
                    };

                    // Add to dictionary, creating list if needed
                    if (!LineAssignments.ContainsKey(lineId))
                    {
                        LineAssignments[lineId] = new List<LineAssignment>();
                    }

                    LineAssignments[lineId].Add(assignment);
                }
            }
        }

        // Inner class to store line assignment data
        public class LineAssignment
        {
            public string LineId { get; set; }
            public string Story { get; set; }
            public string Section { get; set; }
            public string ReleaseCondition { get; set; }
            public bool IsLateral { get; set; }
            public double? ColumnAngle { get; set; }  // Nullable double for column orientation
        }
    }
}
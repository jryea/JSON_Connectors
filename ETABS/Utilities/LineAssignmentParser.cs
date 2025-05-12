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
            _lineAssignments.Clear();

            if (string.IsNullOrWhiteSpace(lineAssignsSection))
                return;

            // Regular expression to match line assignment
            // Example: LINEASSIGN "C1" "Story1" SECTION "Column1" RELEASE "M2I M2J M3I M3J" ANG 90
            var pattern = new Regex(@"^\s*LINEASSIGN\s+""([^""]+)""\s+""([^""]+)""\s+SECTION\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Regex for ANG parameter
            var anglePattern = new Regex(@"ANG\s+([\d\.]+)");

            // Regex for RELEASE parameter
            var releasePattern = new Regex(@"RELEASE\s+""([^""]+)""");

            // Find all line assignments
            var matches = pattern.Matches(lineAssignsSection);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    string lineId = match.Groups[1].Value;
                    string story = match.Groups[2].Value;
                    string section = match.Groups[3].Value;

                    // Get the full line text to extract additional parameters
                    string fullLine = match.Value;

                    // Create line assignment
                    var assignment = new LineAssignment
                    {
                        LineId = lineId,
                        Story = story,
                        Section = section
                    };

                    // Look for release condition
                    var releaseMatch = releasePattern.Match(fullLine);
                    if (releaseMatch.Success && releaseMatch.Groups.Count >= 2)
                    {
                        assignment.ReleaseCondition = releaseMatch.Groups[1].Value;
                    }

                    // Look for column angle
                    var angleMatch = anglePattern.Match(fullLine);
                    if (angleMatch.Success && angleMatch.Groups.Count >= 2)
                    {
                        double angle;
                        if (double.TryParse(angleMatch.Groups[1].Value, out angle))
                        {
                            assignment.ColumnAngle = angle;
                        }
                    }

                    // Determine if it's a lateral element
                    assignment.IsLateral = section.Contains("Lat") ||
                                          section.Contains("LATERAL") ||
                                          section.Contains("SMRF");

                    // Add to dictionary
                    if (!_lineAssignments.ContainsKey(lineId))
                    {
                        _lineAssignments[lineId] = new List<LineAssignment>();
                    }
                    _lineAssignments[lineId].Add(assignment);
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
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ETABS.Import.Utilities
{
    // Utility class to parse line assignments from E2K files
    public class LineAssignmentParser
    {
        // Dictionary to store the line assignments by line ID
        public Dictionary<string, LineAssignment> LineAssignments { get; private set; } = new Dictionary<string, LineAssignment>();

        // Line assignment information
        public class LineAssignment
        {
            public string LineId { get; set; }
            public string Story { get; set; }
            public string Section { get; set; }
            public string ReleaseCondition { get; set; }
            public bool IsLateral { get; set; }
        }

        // Parses the LINE ASSIGNS section from E2K content
        public void ParseLineAssignments(string lineAssignsSection)
        {
            if (string.IsNullOrWhiteSpace(lineAssignsSection))
                return;

            // Regular expression to match line assignment lines
            // Format: LINEASSIGN "B1" "Story1" SECTION "W10X12" MAXSTASPC 24 AUTOMESH "YES" MESHATINTERSECTIONS "YES"
            var basicPattern = new Regex(@"^\s*LINEASSIGN\s+""([^""]+)""\s+""([^""]+)""\s+SECTION\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Additional pattern for releases
            var releasePattern = new Regex(@"RELEASE\s+""([^""]+)""", RegexOptions.Singleline);

            // Pattern for lateral flag (ETABS-specific attribute, not directly in E2K but checking for it)
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

                    // Check for release conditions
                    string release = "";
                    Match releaseMatch = releasePattern.Match(fullLine);
                    if (releaseMatch.Success)
                    {
                        release = releaseMatch.Groups[1].Value;
                    }

                    // Check for lateral flag
                    bool isLateral = false;
                    Match lateralMatch = lateralPattern.Match(fullLine);
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

                    // Add to dictionary (overwrite if already exists)
                    LineAssignments[lineId] = assignment;
                }
            }
        }

        // Gets the section name for a specific line ID
        public string GetSectionName(string lineId)
        {
            if (LineAssignments.TryGetValue(lineId, out LineAssignment assignment))
            {
                return assignment.Section;
            }
            return null;
        }

        // Gets the story name for a specific line ID
        public string GetStoryName(string lineId)
        {
            if (LineAssignments.TryGetValue(lineId, out LineAssignment assignment))
            {
                return assignment.Story;
            }
            return null;
        }
    }
}
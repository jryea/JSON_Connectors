using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace ETABS.Import.Utilities
{
    public class LineAssignmentParser
    {
        // Change the storage model to allow multiple assignments per line ID
        public Dictionary<string, List<LineAssignment>> LineAssignments { get; private set; } = new Dictionary<string, List<LineAssignment>>();

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

            LineAssignments.Clear();

            // Regular expression to match line assignment lines
            // Format: LINEASSIGN "B1" "Story1" SECTION "W10X12" MAXSTASPC 24 AUTOMESH "YES" MESHATINTERSECTIONS "YES"
            var basicPattern = new Regex(@"^\s*LINEASSIGN\s+""([^""]+)""\s+""([^""]+)""\s+SECTION\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Additional pattern for releases
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

                    // Add to dictionary, creating list if needed
                    if (!LineAssignments.ContainsKey(lineId))
                    {
                        LineAssignments[lineId] = new List<LineAssignment>();
                    }

                    LineAssignments[lineId].Add(assignment);
                }
            }
        }

        // Gets the section name for a specific line ID and story
        public string GetSectionName(string lineId, string story)
        {
            if (LineAssignments.TryGetValue(lineId, out var assignments))
            {
                var assignment = assignments.FirstOrDefault(a => a.Story == story);
                if (assignment != null)
                {
                    return assignment.Section;
                }
            }
            return null;
        }

        // Gets all assignments for a specific line ID
        public List<LineAssignment> GetAssignments(string lineId)
        {
            if (LineAssignments.TryGetValue(lineId, out var assignments))
            {
                return assignments;
            }
            return new List<LineAssignment>();
        }
    }
}
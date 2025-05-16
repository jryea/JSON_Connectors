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

            // Pattern for column angle
            var anglePattern = new Regex(@"ANG\s+([\d\.\-]+)", RegexOptions.Singleline);

            // Patterns for property modifiers
            var modAreaPattern = new Regex(@"PROPMODA\s+([\d\.\-]+)", RegexOptions.Singleline);
            var modArea2Pattern = new Regex(@"PROPMODA2\s+([\d\.\-]+)", RegexOptions.Singleline);
            var modArea3Pattern = new Regex(@"PROPMODA3\s+([\d\.\-]+)", RegexOptions.Singleline);
            var modTorsionPattern = new Regex(@"PROPMODT\s+([\d\.\-]+)", RegexOptions.Singleline);
            var modI22Pattern = new Regex(@"PROPMODI22\s+([\d\.\-]+)", RegexOptions.Singleline);
            var modI33Pattern = new Regex(@"PROPMODI33\s+([\d\.\-]+)", RegexOptions.Singleline);
            var modMassPattern = new Regex(@"PROPMODM\s+([\d\.\-]+)", RegexOptions.Singleline);
            var modWeightPattern = new Regex(@"PROPMODW\s+([\d\.\-]+)", RegexOptions.Singleline);

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

                    // Check for column angle
                    double? columnAngle = null;
                    Match angleMatch = anglePattern.Match(completeLine);
                    if (angleMatch.Success)
                    {
                        columnAngle = Convert.ToDouble(angleMatch.Groups[1].Value);
                    }

                    // Create assignment object
                    var assignment = new LineAssignment
                    {
                        LineId = lineId,
                        Story = story,
                        Section = section,
                        ReleaseCondition = release,
                        IsLateral = isLateral,
                        ColumnAngle = columnAngle
                    };

                    // Parse property modifiers
                    Match modAreaMatch = modAreaPattern.Match(completeLine);
                    if (modAreaMatch.Success)
                    {
                        assignment.AreaModifier = Convert.ToDouble(modAreaMatch.Groups[1].Value);
                    }

                    Match modArea2Match = modArea2Pattern.Match(completeLine);
                    if (modArea2Match.Success)
                    {
                        assignment.A22Modifier = Convert.ToDouble(modArea2Match.Groups[1].Value);
                    }

                    Match modArea3Match = modArea3Pattern.Match(completeLine);
                    if (modArea3Match.Success)
                    {
                        assignment.A33Modifier = Convert.ToDouble(modArea3Match.Groups[1].Value);
                    }

                    Match modTorsionMatch = modTorsionPattern.Match(completeLine);
                    if (modTorsionMatch.Success)
                    {
                        assignment.TorsionModifier = Convert.ToDouble(modTorsionMatch.Groups[1].Value);
                    }

                    Match modI22Match = modI22Pattern.Match(completeLine);
                    if (modI22Match.Success)
                    {
                        assignment.I22Modifier = Convert.ToDouble(modI22Match.Groups[1].Value);
                    }

                    Match modI33Match = modI33Pattern.Match(completeLine);
                    if (modI33Match.Success)
                    {
                        assignment.I33Modifier = Convert.ToDouble(modI33Match.Groups[1].Value);
                    }

                    Match modMassMatch = modMassPattern.Match(completeLine);
                    if (modMassMatch.Success)
                    {
                        assignment.MassModifier = Convert.ToDouble(modMassMatch.Groups[1].Value);
                    }

                    Match modWeightMatch = modWeightPattern.Match(completeLine);
                    if (modWeightMatch.Success)
                    {
                        assignment.WeightModifier = Convert.ToDouble(modWeightMatch.Groups[1].Value);
                    }

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

            // Property modifiers - defaulting to 1.0 (no modification)
            public double AreaModifier { get; set; } = 1.0;
            public double A22Modifier { get; set; } = 1.0;
            public double A33Modifier { get; set; } = 1.0;
            public double TorsionModifier { get; set; } = 1.0;
            public double I22Modifier { get; set; } = 1.0;
            public double I33Modifier { get; set; } = 1.0;
            public double MassModifier { get; set; } = 1.0;
            public double WeightModifier { get; set; } = 1.0;
        }
    }
}
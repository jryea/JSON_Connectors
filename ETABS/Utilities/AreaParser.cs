using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace ETABS.Utilities
{
    // Utility class to parse area elements (floors, walls) from E2K files
    public class AreaParser
    {
        // Dictionaries to store the parsed area elements by their ID
        public Dictionary<string, AreaConnectivity> Floors { get; private set; } = new Dictionary<string, AreaConnectivity>();
        public Dictionary<string, AreaConnectivity> Walls { get; private set; } = new Dictionary<string, AreaConnectivity>();
        public Dictionary<string, AreaConnectivity> Openings { get; private set; } = new Dictionary<string, AreaConnectivity>();

        // Dictionary to store area assignments
        public Dictionary<string, List<AreaAssignment>> AreaAssignments { get; private set; } = new Dictionary<string, List<AreaAssignment>>();

        // Area connectivity information
        public class AreaConnectivity
        {
            public string AreaId { get; set; }
            public string Type { get; set; } // FLOOR or PANEL (for walls)
            public List<string> PointIds { get; set; } = new List<string>();
        }

        // Area assignment information
        public class AreaAssignment
        {
            public string AreaId { get; set; }
            public string Story { get; set; }
            public string Section { get; set; }
            public string DiaphragmId { get; set; }
            public string MeshType { get; set; }
            public string CardinalPoint { get; set; }
            public string LoadSet { get; set; }
        }

        // Parses the AREA CONNECTIVITIES section from E2K content
        public void ParseAreaConnectivities(string areaConnectivitiesSection)
        {
            if (string.IsNullOrWhiteSpace(areaConnectivitiesSection))
                return;

            // Regular expression to match area connectivity lines
            // Format: AREA "F1" FLOOR 4 "1" "2" "3" "4" 0 0 0 0
            var pattern = new Regex(@"^\s*AREA\s+""([^""]+)""\s+(FLOOR|PANEL|AREA)\s+(\d+)\s+((?:""[^""]+""(?:\s+|$))+)",
                RegexOptions.Multiline);

            var matches = pattern.Matches(areaConnectivitiesSection);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 5)
                {
                    string areaId = match.Groups[1].Value;
                    string areaType = match.Groups[2].Value;
                    int numPoints = int.Parse(match.Groups[3].Value);
                    string pointsList = match.Groups[4].Value;

                    // Extract point IDs (quoted strings)
                    var pointIdPattern = new Regex(@"""([^""]+)""");
                    var pointMatches = pointIdPattern.Matches(pointsList);

                    var pointIds = new List<string>();
                    foreach (Match pointMatch in pointMatches)
                    {
                        if (pointIds.Count < numPoints) // Only take the specified number of points
                            pointIds.Add(pointMatch.Groups[1].Value);
                    }

                    var areaConnectivity = new AreaConnectivity
                    {
                        AreaId = areaId,
                        Type = areaType,
                        PointIds = pointIds
                    };

                    // Add to the appropriate dictionary based on element type
                    if (areaType.Equals("FLOOR", StringComparison.OrdinalIgnoreCase))
                    {
                        Floors[areaId] = areaConnectivity;
                    }
                    else if (areaType.Equals("PANEL", StringComparison.OrdinalIgnoreCase))
                    {
                        Walls[areaId] = areaConnectivity;
                    }
                }
            }
        }

        // Parses the AREA ASSIGNS section from E2K content
        public void ParseAreaAssignments(string areaAssignsSection)
        {
            if (string.IsNullOrWhiteSpace(areaAssignsSection))
                return;

            // Clear existing assignments
            AreaAssignments.Clear();

            // Regular expression to match area assignment lines
            // Format: AREAASSIGN "F1" "Story1" SECTION "Slab1" OBJMESHTYPE "DEFAULT" ADDRESTRAINT "No" CARDINALPOINT "MIDDLE" TRANSFORMSTIFFNESSFOROFFSETS "No"
            var sectionPattern = new Regex(@"^\s*AREAASSIGN\s+""([^""]+)""\s+""([^""]+)""\s+SECTION\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Pattern for diaphragm assignments
            var diaphragmPattern = new Regex(@"^\s*AREAASSIGN\s+""([^""]+)""\s+""([^""]+)""\s+DIAPH\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Pattern for load assignments
            var loadPattern = new Regex(@"^\s*AREALOAD\s+""([^""]+)""\s+""([^""]+)""\s+TYPE\s+""UNIFLOADSET""\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Pattern for mesh type
            var meshPattern = new Regex(@"OBJMESHTYPE\s+""([^""]+)""", RegexOptions.Singleline);

            // Pattern for cardinal point
            var cardinalPattern = new Regex(@"CARDINALPOINT\s+""([^""]+)""", RegexOptions.Singleline);

            // Process section assignments
            var sectionMatches = sectionPattern.Matches(areaAssignsSection);
            foreach (Match match in sectionMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string areaId = match.Groups[1].Value;
                    string story = match.Groups[2].Value;
                    string section = match.Groups[3].Value;
                    string fullLine = match.Value;

                    // Extract additional properties if present
                    string meshType = "DEFAULT";
                    Match meshMatch = meshPattern.Match(fullLine);
                    if (meshMatch.Success)
                    {
                        meshType = meshMatch.Groups[1].Value;
                    }

                    string cardinalPoint = "MIDDLE";
                    Match cardinalMatch = cardinalPattern.Match(fullLine);
                    if (cardinalMatch.Success)
                    {
                        cardinalPoint = cardinalMatch.Groups[1].Value;
                    }

                    var assignment = new AreaAssignment
                    {
                        AreaId = areaId,
                        Story = story,
                        Section = section,
                        MeshType = meshType,
                        CardinalPoint = cardinalPoint
                    };

                    // Initialize list if needed
                    if (!AreaAssignments.ContainsKey(areaId))
                    {
                        AreaAssignments[areaId] = new List<AreaAssignment>();
                    }

                    AreaAssignments[areaId].Add(assignment);
                }
            }

            // Process diaphragm assignments
            var diaphragmMatches = diaphragmPattern.Matches(areaAssignsSection);
            foreach (Match match in diaphragmMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string areaId = match.Groups[1].Value;
                    string story = match.Groups[2].Value;
                    string diaphragm = match.Groups[3].Value;

                    // Find the existing assignment for this area and story
                    if (AreaAssignments.TryGetValue(areaId, out var assignments))
                    {
                        var existingAssignment = assignments.FirstOrDefault(a => a.Story == story);
                        if (existingAssignment != null)
                        {
                            // Update existing assignment
                            existingAssignment.DiaphragmId = diaphragm;
                        }
                        else
                        {
                            // Create new assignment with just diaphragm
                            assignments.Add(new AreaAssignment
                            {
                                AreaId = areaId,
                                Story = story,
                                DiaphragmId = diaphragm
                            });
                        }
                    }
                    else
                    {
                        // Create new list with assignment
                        AreaAssignments[areaId] = new List<AreaAssignment>
                        {
                            new AreaAssignment
                            {
                                AreaId = areaId,
                                Story = story,
                                DiaphragmId = diaphragm
                            }
                        };
                    }
                }
            }

            // Process load assignments
            var loadMatches = loadPattern.Matches(areaAssignsSection);
            foreach (Match match in loadMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string areaId = match.Groups[1].Value;
                    string story = match.Groups[2].Value;
                    string loadSet = match.Groups[3].Value;

                    // Find the existing assignment for this area and story
                    if (AreaAssignments.TryGetValue(areaId, out var assignments))
                    {
                        var existingAssignment = assignments.FirstOrDefault(a => a.Story == story);
                        if (existingAssignment != null)
                        {
                            // Update existing assignment
                            existingAssignment.LoadSet = loadSet;
                        }
                        else
                        {
                            // Create new assignment with just load set
                            assignments.Add(new AreaAssignment
                            {
                                AreaId = areaId,
                                Story = story,
                                LoadSet = loadSet
                            });
                        }
                    }
                    else
                    {
                        // Create new list with assignment
                        AreaAssignments[areaId] = new List<AreaAssignment>
                        {
                            new AreaAssignment
                            {
                                AreaId = areaId,
                                Story = story,
                                LoadSet = loadSet
                            }
                        };
                    }
                }
            }
        }

        // Gets the section name for a specific area ID and story
        public string GetSectionName(string areaId, string story)
        {
            if (AreaAssignments.TryGetValue(areaId, out var assignments))
            {
                var assignment = assignments.FirstOrDefault(a => a.Story == story);
                if (assignment != null)
                {
                    return assignment.Section;
                }
            }
            return null;
        }

        // Gets the diaphragm ID for a specific area ID and story
        public string GetDiaphragmId(string areaId, string story)
        {
            if (AreaAssignments.TryGetValue(areaId, out var assignments))
            {
                var assignment = assignments.FirstOrDefault(a => a.Story == story);
                if (assignment != null)
                {
                    return assignment.DiaphragmId;
                }
            }
            return null;
        }

        // Gets the load set ID for a specific area ID and story
     
        public string GetLoadSetId(string areaId, string story)
        {
            if (AreaAssignments.TryGetValue(areaId, out var assignments))
            {
                var assignment = assignments.FirstOrDefault(a => a.Story == story);
                if (assignment != null)
                {
                    return assignment.LoadSet;
                }
            }
            return null;
        }

        // Gets all area assignments for a specific area ID

        public List<AreaAssignment> GetAreaAssignments(string areaId)
        {
            if (AreaAssignments.TryGetValue(areaId, out var assignments))
            {
                return assignments;
            }
            return new List<AreaAssignment>();
        }

        // Gets all stories that an area is assigned to
        public List<string> GetAreaStories(string areaId)
        {
            if (AreaAssignments.TryGetValue(areaId, out var assignments))
            {
                return assignments.Select(a => a.Story).Distinct().ToList();
            }
            return new List<string>();
        }
    }
}
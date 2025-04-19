using System;
using System.IO;
using Core.Models;
using Core.Converters;
using Core.Utilities;
using RAM;
using System.Windows.Forms;
using System.Text.Json;
using Core.Models.Elements;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StructuralModelTester
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("Structural Model Tester");
            Console.WriteLine("======================");

            if (args.Length > 0)
            {
                ProcessCommand(args);
            }
            else
            {
                ShowMenu();
            }
        }

        static void ShowMenu()
        {
            try
            {
                // Test simple serialization
                var testObj = new { Name = "Test" };
                string json = System.Text.Json.JsonSerializer.Serialize(testObj);
                Console.WriteLine("Basic JSON serialization works");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON error: {ex.Message}");
            }
            while (true)
            {
                Console.WriteLine("\nSelect an option:");
                Console.WriteLine("1. Convert JSON to RAM");
                Console.WriteLine("2. Convert RAM to JSON");
                Console.WriteLine("3. Convert JSON to E2K");
                Console.WriteLine("4. Analyze Model");
                Console.WriteLine("5. Analyze ETABS E2K file");
                Console.WriteLine("0. Exit");

                Console.Write("\nOption: ");
                string input = Console.ReadLine();

                switch (input)
                {
                    case "0":
                        return;
                    case "1":
                        ConvertJsonToRam();
                        break;
                    case "2":
                        ConvertRamToJson();
                        break;
                    case "3":
                        ConvertJsonToE2K();
                        break;
                    case "4":
                        AnalyzeModel();
                        break;
                    case "5":
                        AnalyzeEtabsFile();
                        break;
                    default:
                        Console.WriteLine("Invalid option.");
                        break;
                }
            }
        }
        

        // Helper method to calculate polygon area
        static double CalculatePolygonArea(List<Core.Models.Geometry.Point3D> points)
        {
            if (points.Count < 3)
                return 0;

            double area = 0;
            int j = points.Count - 1;

            // Using the shoelace formula
            for (int i = 0; i < points.Count; i++)
            {
                area += (points[j].X + points[i].X) * (points[j].Y - points[i].Y);
                j = i;
            }

            return Math.Abs(area / 2);
        }

        static void AnalyzeEtabsFile()
        {
            string e2kPath = BrowseForFile("Select ETABS E2K File", "ETABS files (*.e2k)|*.e2k|All files (*.*)|*.*");
            if (string.IsNullOrEmpty(e2kPath)) return;

            try
            {
                Console.WriteLine($"Analyzing ETABS file: {e2kPath}");

                // Read the E2K file
                string e2kContent = File.ReadAllText(e2kPath);

                // Parse E2K content into sections
                var e2kParser = new ETABS.Utilities.E2KParser();
                Dictionary<string, string> e2kSections = e2kParser.ParseE2K(e2kContent);

                // Display parsed sections
                Console.WriteLine($"\nFound {e2kSections.Count} sections in E2K file");

                // Lists to collect short and duplicate elements for later reporting
                var shortElements = new List<string>();
                var duplicateElements = new List<string>();
                var duplicatePointSets = new List<string>();
                int totalDuplicatePoints = 0;   

                // Get project info if available
                if (e2kSections.TryGetValue("PROJECT INFORMATION", out string projectInfoSection))
                {
                    var projectInfoImporter = new ETABS.Import.Metadata.ETABSToProjectInfo();
                    var projectInfo = projectInfoImporter.Import(projectInfoSection);
                    Console.WriteLine($"Project Name: {projectInfo.ProjectName}");
                    Console.WriteLine($"Project ID: {projectInfo.ProjectId}");
                    Console.WriteLine($"Creation Date: {projectInfo.CreationDate}");
                }

                // Get units if available
                if (e2kSections.TryGetValue("CONTROLS", out string controlsSection))
                {
                    var unitsImporter = new ETABS.Import.Metadata.ETABSToUnits();
                    var units = unitsImporter.Import(controlsSection);
                    Console.WriteLine($"Units - Length: {units.Length}, Force: {units.Force}, Temperature: {units.Temperature}");
                }

                // Parse points first for checking element lengths
                var pointsCollector = new ETABS.Utilities.PointsCollector();
                if (e2kSections.TryGetValue("POINT COORDINATES", out string pointsSection))
                {
                    pointsCollector.ParsePoints(pointsSection);
                    Console.WriteLine($"\nPoints: {pointsCollector.Points.Count}");
                }

                // Check frame elements
                int beamCount = 0, columnCount = 0, braceCount = 0;
                int shortBeams = 0, shortBraces = 0;
                var lineConnectivityParser = new ETABS.Utilities.LineConnectivityParser();

                if (e2kSections.TryGetValue("LINE CONNECTIVITIES", out string lineConnectivitiesSection))
                {
                    lineConnectivityParser.ParseLineConnectivities(lineConnectivitiesSection);

                    beamCount = lineConnectivityParser.Beams.Count;
                    columnCount = lineConnectivityParser.Columns.Count;
                    braceCount = lineConnectivityParser.Braces.Count;

                    // Check for short beams (less than 12 inches / 1 foot)
                    foreach (var entry in lineConnectivityParser.Beams)
                    {
                        string beamId = entry.Key;
                        var beam = entry.Value;
                        var p1 = pointsCollector.GetPoint3D(beam.Point1Id);
                        var p2 = pointsCollector.GetPoint3D(beam.Point2Id);

                        if (p1 != null && p2 != null)
                        {
                            double length = Math.Sqrt(
                                Math.Pow(p2.X - p1.X, 2) +
                                Math.Pow(p2.Y - p1.Y, 2));

                            if (length < 12)
                            {
                                shortBeams++;
                                shortElements.Add($"Beam {beamId}: Length = {length:F2} in, Points: ({p1.X:F2}, {p1.Y:F2}) to ({p2.X:F2}, {p2.Y:F2})");
                            }
                        }
                    }

                    // Check for short braces
                    foreach (var entry in lineConnectivityParser.Braces)
                    {
                        string braceId = entry.Key;
                        var brace = entry.Value;
                        var p1 = pointsCollector.GetPoint3D(brace.Point1Id);
                        var p2 = pointsCollector.GetPoint3D(brace.Point2Id);

                        if (p1 != null && p2 != null)
                        {
                            double length = Math.Sqrt(
                                Math.Pow(p2.X - p1.X, 2) +
                                Math.Pow(p2.Y - p1.Y, 2));

                            if (length < 12)
                            {
                                shortBraces++;
                                shortElements.Add($"Brace {braceId}: Length = {length:F2} in, Points: ({p1.X:F2}, {p1.Y:F2}) to ({p2.X:F2}, {p2.Y:F2})");
                            }
                        }
                    }

                    // Check for duplicate points// Check for duplicate points
                    if (pointsCollector.Points.Count > 0)
                    {
                        // Group points by their coordinates (with a small tolerance)
                        var pointGroups = new Dictionary<string, List<string>>();

                        foreach (var entry in pointsCollector.Points)
                        {
                            string pointId = entry.Key;
                            var point = entry.Value;

                            // Create a rounded coordinate key for grouping similar points
                            string coordKey = $"{Math.Round(point.X, 3)},{Math.Round(point.Y, 3)}";

                            if (!pointGroups.TryGetValue(coordKey, out var pointIds))
                            {
                                pointIds = new List<string>();
                                pointGroups[coordKey] = pointIds;
                            }

                            pointIds.Add(pointId);
                        }

                        // Find groups with more than one point
                        totalDuplicatePoints = 0;
                        foreach (var group in pointGroups)
                        {
                            if (group.Value.Count > 1)
                            {
                                int duplicatesInGroup = group.Value.Count - 1;
                                totalDuplicatePoints += duplicatesInGroup;

                                // Get the coordinates
                                string[] coords = group.Key.Split(',');
                                double x = double.Parse(coords[0]);
                                double y = double.Parse(coords[1]);

                                // Create a detailed entry showing which points are duplicates
                                var pointDetails = new StringBuilder();
                                pointDetails.Append($"Duplicate points at ({x}, {y}):\n");

                                // List each point in the group with its actual coordinates
                                foreach (var pointId in group.Value)
                                {
                                    var exactPoint = pointsCollector.Points[pointId];
                                    pointDetails.Append($"    Point {pointId}: ({exactPoint.X}, {exactPoint.Y})\n");
                                }

                                duplicatePointSets.Add(pointDetails.ToString());
                            }
                        }

                        if (totalDuplicatePoints > 0)
                        {
                            Console.WriteLine($"- Duplicate Points: {totalDuplicatePoints}");
                        }
                    }


                    // Skip empty sections
                    if (beamCount > 0 || columnCount > 0 || braceCount > 0)
                    {
                        Console.WriteLine($"\nFrame Elements:");
                        if (beamCount > 0) Console.WriteLine($"- Beams: {beamCount}");
                        if (columnCount > 0) Console.WriteLine($"- Columns: {columnCount}");
                        if (braceCount > 0) Console.WriteLine($"- Braces: {braceCount}");
                    }
                }

                // Check for duplicate line elements by analyzing line assignments
                var lineAssignmentParser = new ETABS.Utilities.LineAssignmentParser();
                var duplicateBeams = 0;
                var duplicateColumns = 0;
                var duplicateBraces = 0;

                if (e2kSections.TryGetValue("LINE ASSIGNS", out string lineAssignsSection))
                {
                    lineAssignmentParser.ParseLineAssignments(lineAssignsSection);

                    // Dictionary to track elements based on geometry and story
                    var beamKeys = new Dictionary<string, List<string>>();
                    var columnKeys = new Dictionary<string, List<string>>();
                    var braceKeys = new Dictionary<string, List<string>>();

                    // Check beam assignments
                    foreach (var beamId in lineConnectivityParser.Beams.Keys)
                    {
                        if (lineAssignmentParser.LineAssignments.TryGetValue(beamId, out var assignments))
                        {
                            var beam = lineConnectivityParser.Beams[beamId];
                            var p1 = pointsCollector.GetPoint3D(beam.Point1Id);
                            var p2 = pointsCollector.GetPoint3D(beam.Point2Id);

                            if (p1 == null || p2 == null) continue;

                            // Create geometry key (normalized for direction)
                            string geoKey;
                            if (p1.X < p2.X || (p1.X == p2.X && p1.Y < p2.Y))
                            {
                                geoKey = $"{p1.X:F2},{p1.Y:F2}_{p2.X:F2},{p2.Y:F2}";
                            }
                            else
                            {
                                geoKey = $"{p2.X:F2},{p2.Y:F2}_{p1.X:F2},{p1.Y:F2}";
                            }

                            // Check each story assignment
                            foreach (var assignment in assignments)
                            {
                                string storyKey = $"{geoKey}_{assignment.Story}";

                                if (!beamKeys.TryGetValue(storyKey, out var storyElements))
                                {
                                    storyElements = new List<string>();
                                    beamKeys[storyKey] = storyElements;
                                }

                                // If we already have this beam at this story, it's a duplicate
                                if (storyElements.Count > 0)
                                {
                                    duplicateBeams++;
                                    string existingBeamId = storyElements[0];
                                    duplicateElements.Add($"Duplicate Beam: {beamId} duplicates {existingBeamId} at {assignment.Story}");
                                }

                                storyElements.Add(beamId);
                            }
                        }
                    }

                    // Check column assignments
                    foreach (var columnId in lineConnectivityParser.Columns.Keys)
                    {
                        if (lineAssignmentParser.LineAssignments.TryGetValue(columnId, out var assignments))
                        {
                            var column = lineConnectivityParser.Columns[columnId];
                            var p1 = pointsCollector.GetPoint3D(column.Point1Id);

                            if (p1 == null) continue;

                            // For columns, use the start point and story as the key
                            foreach (var assignment in assignments)
                            {
                                string geoKey = $"{p1.X:F2},{p1.Y:F2}_{assignment.Story}";

                                if (!columnKeys.TryGetValue(geoKey, out var storyElements))
                                {
                                    storyElements = new List<string>();
                                    columnKeys[geoKey] = storyElements;
                                }

                                // If we already have this column at this story, it's a duplicate
                                if (storyElements.Count > 0)
                                {
                                    duplicateColumns++;
                                    string existingColumnId = storyElements[0];
                                    duplicateElements.Add($"Duplicate Column: {columnId} duplicates {existingColumnId} at {assignment.Story}");
                                }

                                storyElements.Add(columnId);
                            }
                        }
                    }


                    // Check brace assignments
                    foreach (var braceId in lineConnectivityParser.Braces.Keys)
                    {
                        if (lineAssignmentParser.LineAssignments.TryGetValue(braceId, out var assignments))
                        {
                            var brace = lineConnectivityParser.Braces[braceId];
                            var p1 = pointsCollector.GetPoint3D(brace.Point1Id);
                            var p2 = pointsCollector.GetPoint3D(brace.Point2Id);

                            if (p1 == null || p2 == null) continue;

                            // Create geometry key (normalized for direction)
                            string geoKey;
                            if (p1.X < p2.X || (p1.X == p2.X && p1.Y < p2.Y))
                            {
                                geoKey = $"{p1.X:F2},{p1.Y:F2}_{p2.X:F2},{p2.Y:F2}";
                            }
                            else
                            {
                                geoKey = $"{p2.X:F2},{p2.Y:F2}_{p1.X:F2},{p1.Y:F2}";
                            }

                            // Check each story assignment
                            foreach (var assignment in assignments)
                            {
                                string storyKey = $"{geoKey}_{assignment.Story}";

                                if (!braceKeys.TryGetValue(storyKey, out var storyElements))
                                {
                                    storyElements = new List<string>();
                                    braceKeys[storyKey] = storyElements;
                                }

                                // If we already have this brace at this story, it's a duplicate
                                if (storyElements.Count > 0)
                                {
                                    duplicateBraces++;
                                    string existingBraceId = storyElements[0];
                                    duplicateElements.Add($"Duplicate Brace: {braceId} duplicates {existingBraceId} at {assignment.Story}");
                                }

                                storyElements.Add(braceId);
                            }
                        }
                    }
                }

                // Count wall and floor elements
                int wallCount = 0, floorCount = 0;
                int smallWalls = 0, smallFloors = 0;
                var areaParser = new ETABS.Utilities.AreaParser();

                if (e2kSections.TryGetValue("AREA CONNECTIVITIES", out string areaConnectivitiesSection))
                {
                    areaParser.ParseAreaConnectivities(areaConnectivitiesSection);

                    wallCount = areaParser.Walls.Count;
                    floorCount = areaParser.Floors.Count;

                    // Check for small walls (area < 1 sq ft)
                    foreach (var entry in areaParser.Walls)
                    {
                        string wallId = entry.Key;
                        var wall = entry.Value;
                        var points = new List<Core.Models.Geometry.Point3D>();
                        foreach (var pointId in wall.PointIds)
                        {
                            var point = pointsCollector.GetPoint3D(pointId);
                            if (point != null)
                            {
                                points.Add(point);
                            }
                        }

                        if (points.Count >= 3)
                        {
                            // Calculate approximate area
                            double area = CalculatePolygonArea(points);
                            if (area < 144) // 1 sq ft = 144 sq in
                            {
                                smallWalls++;
                                shortElements.Add($"Wall {wallId}: Area = {area:F2} sq in, Points = {points.Count}");
                            }
                        }
                    }

                    // Check for small floors
                    foreach (var entry in areaParser.Floors)
                    {
                        string floorId = entry.Key;
                        var floor = entry.Value;
                        var points = new List<Core.Models.Geometry.Point3D>();
                        foreach (var pointId in floor.PointIds)
                        {
                            var point = pointsCollector.GetPoint3D(pointId);
                            if (point != null)
                            {
                                points.Add(point);
                            }
                        }

                        if (points.Count >= 3)
                        {
                            // Calculate approximate area
                            double area = CalculatePolygonArea(points);
                            if (area < 144) // 1 sq ft = 144 sq in
                            {
                                smallFloors++;
                                shortElements.Add($"Floor {floorId}: Area = {area:F2} sq in, Points = {points.Count}");
                            }
                        }
                    }

                    // Skip empty sections
                    if (wallCount > 0 || floorCount > 0)
                    {
                        Console.WriteLine($"\nArea Elements:");
                        if (wallCount > 0) Console.WriteLine($"- Walls: {wallCount}");
                        if (floorCount > 0) Console.WriteLine($"- Floors: {floorCount}");
                    }
                }

                // Check for duplicate area elements by analyzing area assignments
                var duplicateWalls = 0;
                var duplicateFloors = 0;

                if (e2kSections.TryGetValue("AREA ASSIGNS", out string areaAssignsSection))
                {
                    areaParser.ParseAreaAssignments(areaAssignsSection);

                    // Dictionary to track elements based on geometry and story
                    var wallKeys = new Dictionary<string, List<string>>();
                    var floorKeys = new Dictionary<string, List<string>>();

                    // Check wall assignments
                    foreach (var wallId in areaParser.Walls.Keys)
                    {
                        var wallConnectivity = areaParser.Walls[wallId];
                        var assignments = areaParser.GetAreaAssignments(wallId);

                        if (assignments.Count == 0) continue;

                        // Create a geometric key based on point IDs (sorted for consistency)
                        var pointIdsList = new List<string>(wallConnectivity.PointIds);
                        pointIdsList.Sort();
                        string geoKey = string.Join("_", pointIdsList);

                        // Check each story assignment
                        foreach (var assignment in assignments)
                        {
                            string storyKey = $"{geoKey}_{assignment.Story}";

                            if (!wallKeys.TryGetValue(storyKey, out var storyElements))
                            {
                                storyElements = new List<string>();
                                wallKeys[storyKey] = storyElements;
                            }

                            // If we already have this wall at this story, it's a duplicate
                            if (storyElements.Count > 0)
                            {
                                duplicateWalls++;
                                string existingWallId = storyElements[0];
                                duplicateElements.Add($"Duplicate Wall: {wallId} duplicates {existingWallId} at {assignment.Story}");
                            }

                            storyElements.Add(wallId);
                        }
                    }

                    // Check floor assignments
                    foreach (var floorId in areaParser.Floors.Keys)
                    {
                        var floorConnectivity = areaParser.Floors[floorId];
                        var assignments = areaParser.GetAreaAssignments(floorId);

                        if (assignments.Count == 0) continue;

                        // Create a geometric key based on point IDs (sorted for consistency)
                        var pointIdsList = new List<string>(floorConnectivity.PointIds);
                        pointIdsList.Sort();
                        string geoKey = string.Join("_", pointIdsList);

                        // Check each story assignment
                        foreach (var assignment in assignments)
                        {
                            string storyKey = $"{geoKey}_{assignment.Story}";

                            if (!floorKeys.TryGetValue(storyKey, out var storyElements))
                            {
                                storyElements = new List<string>();
                                floorKeys[storyKey] = storyElements;
                            }

                            // If we already have this floor at this story, it's a duplicate
                            if (storyElements.Count > 0)
                            {
                                duplicateFloors++;
                                string existingFloorId = storyElements[0];
                                duplicateElements.Add($"Duplicate Floor: {floorId} duplicates {existingFloorId} at {assignment.Story}");
                            }

                            storyElements.Add(floorId);
                        }
                    }
                }

                // Count property types
                if (e2kSections.TryGetValue("FRAME SECTIONS", out string frameSectionsSection))
                {
                    // Count frame section types by counting unique section names
                    var sectionPattern = new System.Text.RegularExpressions.Regex(@"FRAMESECTION\s+""([^""]+)""");
                    var matches = sectionPattern.Matches(frameSectionsSection);
                    var uniqueSections = new HashSet<string>();

                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Groups.Count >= 2)
                        {
                            uniqueSections.Add(match.Groups[1].Value);
                        }
                    }

                    if (uniqueSections.Count > 0)
                    {
                        Console.WriteLine($"\nProperties:");
                        Console.WriteLine($"- Frame Sections: {uniqueSections.Count}");
                    }
                }

                // Count levels
                int levelCount = 0;
                if (e2kSections.TryGetValue("STORIES - IN SEQUENCE FROM TOP", out string storiesSection))
                {
                    var storyPattern = new System.Text.RegularExpressions.Regex(@"STORY\s+""([^""]+)""");
                    var matches = storyPattern.Matches(storiesSection);
                    levelCount = matches.Count;

                    if (levelCount > 0)
                    {
                        Console.WriteLine($"\nModel Layout:");
                        Console.WriteLine($"- Levels: {levelCount}");
                    }
                }

                // Count grid lines
                if (e2kSections.TryGetValue("GRIDS", out string gridsSection))
                {
                    var gridPattern = new System.Text.RegularExpressions.Regex(@"GRID\s+""([^""]+)""\s+LABEL");
                    var matches = gridPattern.Matches(gridsSection);

                    if (matches.Count > 0)
                    {
                        if (levelCount == 0)
                        {
                            Console.WriteLine($"\nModel Layout:");
                        }
                        Console.WriteLine($"- Grids: {matches.Count}");
                    }
                }

                // Display warnings at the end - only if there are any
                bool hasWarnings = (shortBeams + shortBraces + smallWalls + smallFloors +
                                   duplicateBeams + duplicateColumns + duplicateBraces +
                                   duplicateWalls + duplicateFloors + totalDuplicatePoints) > 0;

                if (hasWarnings)
                {
                    Console.WriteLine("\n===== WARNINGS =====");

                    // Display summary of short elements
                    int totalShortElements = shortBeams + shortBraces + smallWalls + smallFloors;
                    if (totalShortElements > 0)
                    {
                        Console.WriteLine($"\n⚠️ SHORT ELEMENTS ({totalShortElements}):");
                        if (shortBeams > 0) Console.WriteLine($"- Short Beams: {shortBeams}");
                        if (shortBraces > 0) Console.WriteLine($"- Short Braces: {shortBraces}");
                        if (smallWalls > 0) Console.WriteLine($"- Small Walls: {smallWalls}");
                        if (smallFloors > 0) Console.WriteLine($"- Small Floors: {smallFloors}");

                        // Display detailed list of short elements (limited to first 20 for readability)
                        if (shortElements.Count > 0)
                        {
                            Console.WriteLine("\nShort Element Details (first 20):");
                            for (int i = 0; i < Math.Min(20, shortElements.Count); i++)
                            {
                                Console.WriteLine($"  {shortElements[i]}");
                            }

                            if (shortElements.Count > 20)
                            {
                                Console.WriteLine($"  ... and {shortElements.Count - 20} more");
                            }
                        }

                    }

                    // Display duplicate points
                    if (totalDuplicatePoints > 0)
                    {
                        Console.WriteLine($"DUPLICATE POINTS ({totalDuplicatePoints}):");
                        Console.WriteLine("The following points have identical or nearly identical coordinates:");

                        // Show all duplicate point sets
                        foreach (var pointSetDetails in duplicatePointSets)
                        {
                            Console.WriteLine(pointSetDetails);
                        }
                    }

                    // Display summary of duplicate elements
                    int totalDuplicateElements = duplicateBeams + duplicateColumns + duplicateBraces + duplicateWalls + duplicateFloors;
                    if (totalDuplicateElements > 0)
                    {
                        Console.WriteLine($"\nDUPLICATE ELEMENTS ({totalDuplicateElements}):");
                        if (duplicateBeams > 0) Console.WriteLine($"- Duplicate Beams: {duplicateBeams}");
                        if (duplicateColumns > 0) Console.WriteLine($"- Duplicate Columns: {duplicateColumns}");
                        if (duplicateBraces > 0) Console.WriteLine($"- Duplicate Braces: {duplicateBraces}");
                        if (duplicateWalls > 0) Console.WriteLine($"- Duplicate Walls: {duplicateWalls}");
                        if (duplicateFloors > 0) Console.WriteLine($"- Duplicate Floors: {duplicateFloors}");

                        // Display detailed list of duplicate elements (limited to first 20 for readability)
                        if (duplicateElements.Count > 0)
                        {
                            Console.WriteLine("\nDuplicate Element Details (first 20):");
                            for (int i = 0; i < Math.Min(20, duplicateElements.Count); i++)
                            {
                                Console.WriteLine($"  {duplicateElements[i]}");
                            }

                            if (duplicateElements.Count > 20)
                            {
                                Console.WriteLine($"  ... and {duplicateElements.Count - 20} more");
                            }
                        }
                    }
                }

                Console.WriteLine("\nAnalysis complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void ConvertJsonToRam()
        {
            string jsonPath = BrowseForFile("Select JSON File", "JSON files (*.json)|*.json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            string ramPath = BrowseForFile("Save RAM File", "RAM files (*.rss)|*.ram|All files (*.*)|*.*", true);
            if (string.IsNullOrEmpty(ramPath)) return;

            try
            {
                var converter = new RAMImporter();
                var result = converter.ConvertJSONFileToRAM(jsonPath, ramPath);

                Console.WriteLine(result.Success ? "Success" : "Failed");
                Console.WriteLine(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void ConvertRamToJson()
        {
            Console.Write("Input RAM file path: ");
            string ramPath = Console.ReadLine();
            Console.Write("Output JSON file path: ");
            string jsonPath = Console.ReadLine();

            if (!File.Exists(ramPath))
            {
                Console.WriteLine($"File not found: {ramPath}");
                return;
            }

            try
            {
                var converter = new RAMExporter();
                var result = converter.ConvertRAMToJSON(ramPath);

                if (result.Success && !string.IsNullOrEmpty(result.JsonOutput))
                {
                    File.WriteAllText(jsonPath, result.JsonOutput);
                    Console.WriteLine("Success");
                }
                else
                {
                    Console.WriteLine("Failed");
                }

                Console.WriteLine(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void AnalyzeModel()
        {
            string jsonPath = BrowseForFile("Select Input JSON File");
            if (string.IsNullOrEmpty(jsonPath)) return;

            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"File not found: {jsonPath}");
                return;
            }

            try
            {
                BaseModel model = JsonConverter.LoadFromFile(jsonPath);

                Console.WriteLine("\nModel Analysis:");
                Console.WriteLine($"Project: {model.Metadata?.ProjectInfo?.ProjectName ?? "Unknown"}");
                Console.WriteLine($"Units: {model.Metadata?.Units?.Length ?? "Unknown"}");
                Console.WriteLine("\nElements:");
                Console.WriteLine($"- Beams: {model.Elements.Beams?.Count ?? 0}");
                Console.WriteLine($"- Columns: {model.Elements.Columns?.Count ?? 0}");
                Console.WriteLine($"- Walls: {model.Elements.Walls?.Count ?? 0}");
                Console.WriteLine($"- Floors: {model.Elements.Floors?.Count ?? 0}");
                Console.WriteLine("\nModel Layout:");
                Console.WriteLine($"- Levels: {model.ModelLayout.Levels?.Count ?? 0}");
                Console.WriteLine($"- Grids: {model.ModelLayout.Grids?.Count ?? 0}");
                Console.WriteLine($"- Floor Types: {model.ModelLayout.FloorTypes?.Count ?? 0}");

                // Validation Checks
                Console.WriteLine("\nValidation Checks:");

                // Check for duplicate beams
                ValidateDuplicateBeams(model);

                // Check for duplicate columns
                ValidateDuplicateColumns(model);

                // Check for small elements
                ValidateSmallElements(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void ConvertJsonToE2K()
        {
            string jsonPath = BrowseForFile("Select JSON File", "JSON files (*.json)|*.json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            string e2kPath = BrowseForFile("Save E2K File", "ETABS files (*.e2k)|*.e2k|All files (*.*)|*.*", true);
            if (string.IsNullOrEmpty(e2kPath)) return;

            try
            {
                Console.WriteLine("Converting JSON to E2K...");

                // Load the JSON file content
                string jsonContent = File.ReadAllText(jsonPath);

                // Create the converter and process the model
                var converter = new ETABS.GrasshopperToETABS();
                string e2kContent = converter.ProcessModel(jsonContent, null, null);

                // Save the E2K content to file
                File.WriteAllText(e2kPath, e2kContent);

                Console.WriteLine($"Successfully converted JSON to E2K and saved to {e2kPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }
            }
        }

        static void ValidateDuplicateBeams(BaseModel model)
        {
            if (model.Elements.Beams == null || model.Elements.Beams.Count == 0)
                return;

            Console.WriteLine("\nChecking for duplicate beams...");

            int duplicateCount = 0;
            var processedBeams = new Dictionary<string, List<Beam>>();

            foreach (var beam in model.Elements.Beams)
            {
                if (beam == null || beam.StartPoint == null || beam.EndPoint == null)
                    continue;

                // Create key based on endpoints and level
                string key = $"{beam.StartPoint.X},{beam.StartPoint.Y}_{beam.EndPoint.X},{beam.EndPoint.Y}_{beam.LevelId}";
                string reverseKey = $"{beam.EndPoint.X},{beam.EndPoint.Y}_{beam.StartPoint.X},{beam.StartPoint.Y}_{beam.LevelId}";

                if (processedBeams.ContainsKey(key) || processedBeams.ContainsKey(reverseKey))
                {
                    duplicateCount++;
                    string existingKey = processedBeams.ContainsKey(key) ? key : reverseKey;

                    Console.WriteLine($"Duplicate Beam: ID={beam.Id}, Level={beam.LevelId}");
                    Console.WriteLine($"  Duplicates: {string.Join(", ", processedBeams[existingKey].Select(b => b.Id))}");
                }
                else
                {
                    // Add to dictionary
                    if (!processedBeams.ContainsKey(key))
                        processedBeams[key] = new List<Beam>();

                    processedBeams[key].Add(beam);
                }
            }

            Console.WriteLine($"Found {duplicateCount} duplicate beams.");
        }

        static void ValidateDuplicateColumns(BaseModel model)
        {
            if (model.Elements.Columns == null || model.Elements.Columns.Count == 0 ||
                model.ModelLayout.Levels == null || model.ModelLayout.Levels.Count == 0)
                return;

            Console.WriteLine("\nChecking for duplicate columns...");

            int duplicateCount = 0;
            var processedColumns = new Dictionary<string, List<Column>>();

            foreach (var column in model.Elements.Columns)
            {
                if (column == null || column.StartPoint == null)
                    continue;

                // Create key based on start point, base level, and top level
                string key = $"{column.StartPoint.X},{column.StartPoint.Y}_{column.BaseLevelId}_{column.TopLevelId}";

                if (processedColumns.ContainsKey(key))
                {
                    duplicateCount++;
                    Console.WriteLine($"Duplicate Column: ID={column.Id}, Levels={column.BaseLevelId} to {column.TopLevelId}");
                    Console.WriteLine($"  Duplicates: {string.Join(", ", processedColumns[key].Select(c => c.Id))}");
                }
                else
                {
                    // Add to dictionary
                    if (!processedColumns.ContainsKey(key))
                        processedColumns[key] = new List<Column>();

                    processedColumns[key].Add(column);
                }
            }

            Console.WriteLine($"Found {duplicateCount} duplicate columns.");
        }

        static void ValidateSmallElements(BaseModel model)
        {
            Console.WriteLine("\nChecking for elements shorter than 1 foot...");

            int smallBeamCount = 0;
            int smallColumnCount = 0;
            int smallWallCount = 0;

            // Check beams
            if (model.Elements.Beams != null)
            {
                foreach (var beam in model.Elements.Beams)
                {
                    if (beam?.StartPoint == null || beam?.EndPoint == null)
                        continue;

                    double length = Math.Sqrt(
                        Math.Pow(beam.EndPoint.X - beam.StartPoint.X, 2) +
                        Math.Pow(beam.EndPoint.Y - beam.StartPoint.Y, 2));

                    if (length < 1.0) // Assuming model units are in feet
                    {
                        smallBeamCount++;
                        Console.WriteLine($"Small Beam: ID={beam.Id}, Length={length:F2} ft, Level={beam.LevelId}");
                        Console.WriteLine($"  Coordinates: ({beam.StartPoint.X:F2},{beam.StartPoint.Y:F2}) to ({beam.EndPoint.X:F2},{beam.EndPoint.Y:F2})");
                    }
                }
            }

            // Check columns - using level elevations for height
            if (model.Elements.Columns != null && model.ModelLayout.Levels != null)
            {
                var levelsById = model.ModelLayout.Levels.ToDictionary(l => l.Id, l => l);

                foreach (var column in model.Elements.Columns)
                {
                    if (column?.BaseLevelId == null || column?.TopLevelId == null)
                        continue;

                    // Get base and top levels
                    if (!levelsById.TryGetValue(column.BaseLevelId, out var baseLevel) ||
                        !levelsById.TryGetValue(column.TopLevelId, out var topLevel))
                        continue;

                    double height = Math.Abs(topLevel.Elevation - baseLevel.Elevation);

                    if (height < 1.0) // Assuming model units are in feet
                    {
                        smallColumnCount++;
                        Console.WriteLine($"Small Column: ID={column.Id}, Height={height:F2} ft");
                        Console.WriteLine($"  From Level: {baseLevel.Name} (Elev: {baseLevel.Elevation:F2}) to {topLevel.Name} (Elev: {topLevel.Elevation:F2})");
                        Console.WriteLine($"  Location: ({column.StartPoint.X:F2},{column.StartPoint.Y:F2})");
                    }
                }
            }

            // Check walls
            if (model.Elements.Walls != null)
            {
                foreach (var wall in model.Elements.Walls)
                {
                    if (wall?.Points == null || wall.Points.Count < 2)
                        continue;

                    for (int i = 0; i < wall.Points.Count - 1; i++)
                    {
                        var p1 = wall.Points[i];
                        var p2 = wall.Points[i + 1];

                        double length = Math.Sqrt(
                            Math.Pow(p2.X - p1.X, 2) +
                            Math.Pow(p2.Y - p1.Y, 2));

                        if (length < 1.0) // Assuming model units are in feet
                        {
                            smallWallCount++;
                            Console.WriteLine($"Small Wall Segment: ID={wall.Id}, Length={length:F2} ft, Levels={wall.BaseLevelId} to {wall.TopLevelId}");
                            Console.WriteLine($"  Segment: ({p1.X:F2},{p1.Y:F2}) to ({p2.X:F2},{p2.Y:F2})");
                        }
                    }
                }
            }

            Console.WriteLine($"Found {smallBeamCount} small beams, {smallColumnCount} small columns, and {smallWallCount} small wall segments.");
        }
        static void ProcessCommand(string[] args)
        {
            string command = args[0].ToLower();

            switch (command)
            {
                case "duplicates":
                    if (args.Length >= 3)
                        TestRemoveDuplicates(args[1], args[2]);
                    else
                        Console.WriteLine("Usage: duplicates <inputPath> <outputPath>");
                    break;

                // Add more command options here

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    break;
            }
        }

        static void TestRemoveDuplicates(string inputPath, string outputPath)
        {
            // Same implementation as the interactive version
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"File not found: {inputPath}");
                return;
            }

            try
            {
                BaseModel model = JsonConverter.LoadFromFile(inputPath);

                int beamsBefore = model.Elements.Beams?.Count ?? 0;
                int columnsBefore = model.Elements.Columns?.Count ?? 0;
                int wallsBefore = model.Elements.Walls?.Count ?? 0;

                Console.WriteLine($"Before: {beamsBefore} beams, {columnsBefore} columns, {wallsBefore} walls");

                model.RemoveDuplicateGeometry();

                int beamsAfter = model.Elements.Beams?.Count ?? 0;
                int columnsAfter = model.Elements.Columns?.Count ?? 0;
                int wallsAfter = model.Elements.Walls?.Count ?? 0;

                Console.WriteLine($"After: {beamsAfter} beams, {columnsAfter} columns, {wallsAfter} walls");
                Console.WriteLine($"Removed: {beamsBefore - beamsAfter} beams, {columnsBefore - columnsAfter} columns, {wallsBefore - wallsAfter} walls");

                JsonConverter.SaveToFile(model, outputPath);

                Console.WriteLine($"Saved cleaned model to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static string BrowseForFile(string title, string filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", bool saveDialog = false)
        {
            string filePath = "";

            if (saveDialog)
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Title = title;
                    dialog.Filter = filter;
                    dialog.RestoreDirectory = true;

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        filePath = dialog.FileName;
                    }
                }
            }
            else
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = title;
                    dialog.Filter = filter;
                    dialog.RestoreDirectory = true;

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        filePath = dialog.FileName;
                    }
                }
            }

            return filePath;
        }
    }
}
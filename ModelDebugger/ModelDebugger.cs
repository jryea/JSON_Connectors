using Core.Models.ModelLayout;
using Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace ModelDebugger
{
    internal class ModelDebugger
    {
        private const double CoordinateTolerance = 1.0;

        internal static void RunModelDebugger(string[] args)
        {
            string jsonFilePath = "";

            // If no args provided, use file dialog
            if (args.Length == 0)
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                    openFileDialog.RestoreDirectory = true;
                    openFileDialog.Title = "Select Model JSON File";

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        jsonFilePath = openFileDialog.FileName;
                    }
                    else
                    {
                        Console.WriteLine("No file selected. Exiting...");
                        return;
                    }
                }
            }
            else
            {
                jsonFilePath = args[0];
            }

            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    Console.WriteLine($"File not found: {jsonFilePath}");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"Analyzing JSON file: {Path.GetFileName(jsonFilePath)}");

                string jsonContent = File.ReadAllText(jsonFilePath);
                var model = JsonSerializer.Deserialize<BaseModel>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                Console.WriteLine($"Model loaded successfully from {jsonFilePath}");
                Console.WriteLine();

                // Process the model and generate various reports
                AnalyzeModel(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing JSON file: {ex.Message}");
            }

            // Keep console window open
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void AnalyzeModel(BaseModel model)
        {
            if (model == null)
            {
                Console.WriteLine("ERROR: Model is null");
                return;
            }

            Console.WriteLine("=== MODEL SUMMARY ===");
            Console.WriteLine($"Project Name: {model.Metadata?.ProjectInfo?.ProjectName ?? "N/A"}");
            Console.WriteLine($"Units: {model.Metadata?.Units?.Length ?? "N/A"}");
            Console.WriteLine();

            // Analyze Floor Types
            AnalyzeFloorTypes(model);

            // Analyze Levels and their Floor Type relationships
            AnalyzeLevels(model);

            // Check for duplicate geometry
            AnalyzeDuplicateGeometry(model);

            // Analyze Beams and their relationships
            AnalyzeBeams(model);

            // Analyze Columns and their relationships
            AnalyzeColumns(model);

            // Analyze Braces and their relationships
            AnalyzeBraces(model);

            // Analyze Frame Properties
            AnalyzeFrameProperties(model);
        }

        static void AnalyzeDuplicateGeometry(BaseModel model)
        {
            Console.WriteLine("=== DUPLICATE GEOMETRY ANALYSIS ===");

            if (model.Elements == null)
            {
                Console.WriteLine("No elements found to analyze");
                return;
            }

            // Create level lookup for displaying names with IDs
            var levelLookup = new Dictionary<string, string>();
            if (model.ModelLayout?.Levels != null)
            {
                foreach (var level in model.ModelLayout.Levels)
                {
                    levelLookup[level.Id] = level.Name;
                }
            }

            // Check for duplicate floors
            CheckDuplicateFloors(model.Elements.Floors, levelLookup);

            // Check for duplicate walls
            CheckDuplicateWalls(model.Elements.Walls, levelLookup);

            // Check for duplicate beams
            CheckDuplicateBeams(model.Elements.Beams, levelLookup);

            // Check for duplicate columns
            CheckDuplicateColumns(model.Elements.Columns, levelLookup);

            // Check for duplicate braces
            CheckDuplicateBraces(model.Elements.Braces, levelLookup);

            Console.WriteLine();
        }

        // Helper method to format level information
        static string FormatLevelInfo(string levelId, Dictionary<string, string> levelLookup)
        {
            if (string.IsNullOrEmpty(levelId))
                return "Unknown Level";

            if (levelLookup.TryGetValue(levelId, out string levelName))
                return $"Level: {levelName} (ID: {levelId})";

            return $"Level: Unknown (ID: {levelId})";
        }

        static void CheckDuplicateFloors(List<Core.Models.Elements.Floor> floors, Dictionary<string, string> levelLookup)
        {
            if (floors == null || floors.Count == 0)
            {
                Console.WriteLine("No floors to check for duplicates");
                return;
            }

            var duplicateGroups = new List<List<Core.Models.Elements.Floor>>();
            var processed = new HashSet<string>();

            for (int i = 0; i < floors.Count; i++)
            {
                if (processed.Contains(floors[i].Id)) continue;

                var duplicates = new List<Core.Models.Elements.Floor> { floors[i] };
                processed.Add(floors[i].Id);

                for (int j = i + 1; j < floors.Count; j++)
                {
                    if (processed.Contains(floors[j].Id)) continue;

                    if (AreFloorsGeometricallyEqual(floors[i], floors[j]))
                    {
                        duplicates.Add(floors[j]);
                        processed.Add(floors[j].Id);
                    }
                }

                if (duplicates.Count > 1)
                {
                    duplicateGroups.Add(duplicates);
                }
            }

            if (duplicateGroups.Count > 0)
            {
                Console.WriteLine($"DUPLICATE FLOORS FOUND: {duplicateGroups.Count} groups");
                for (int i = 0; i < duplicateGroups.Count; i++)
                {
                    var group = duplicateGroups[i];
                    var levelInfo = FormatLevelInfo(group[0].LevelId, levelLookup);
                    Console.WriteLine($"  Group {i + 1}: {group.Count} duplicate floors on {levelInfo}");
                    foreach (var floor in group)
                    {
                        Console.WriteLine($"    Floor ID: {floor.Id} - Points: {floor.Points?.Count ?? 0}");
                    }
                }
            }
            else
            {
                Console.WriteLine("No duplicate floors found");
            }
        }

        static void CheckDuplicateWalls(List<Core.Models.Elements.Wall> walls, Dictionary<string, string> levelLookup)
        {
            if (walls == null || walls.Count == 0)
            {
                Console.WriteLine("No walls to check for duplicates");
                return;
            }

            var duplicateGroups = new List<List<Core.Models.Elements.Wall>>();
            var processed = new HashSet<string>();

            for (int i = 0; i < walls.Count; i++)
            {
                if (processed.Contains(walls[i].Id)) continue;

                var duplicates = new List<Core.Models.Elements.Wall> { walls[i] };
                processed.Add(walls[i].Id);

                for (int j = i + 1; j < walls.Count; j++)
                {
                    if (processed.Contains(walls[j].Id)) continue;

                    if (AreWallsGeometricallyEqual(walls[i], walls[j]))
                    {
                        duplicates.Add(walls[j]);
                        processed.Add(walls[j].Id);
                    }
                }

                if (duplicates.Count > 1)
                {
                    duplicateGroups.Add(duplicates);
                }
            }

            if (duplicateGroups.Count > 0)
            {
                Console.WriteLine($"DUPLICATE WALLS FOUND: {duplicateGroups.Count} groups");
                for (int i = 0; i < duplicateGroups.Count; i++)
                {
                    var group = duplicateGroups[i];
                    var baseLevelInfo = FormatLevelInfo(group[0].BaseLevelId, levelLookup);
                    var topLevelInfo = FormatLevelInfo(group[0].TopLevelId, levelLookup);
                    Console.WriteLine($"  Group {i + 1}: {group.Count} duplicate walls between {baseLevelInfo} and {topLevelInfo}");
                    foreach (var wall in group)
                    {
                        Console.WriteLine($"    Wall ID: {wall.Id} - Points: {wall.Points?.Count ?? 0}");
                    }
                }
            }
            else
            {
                Console.WriteLine("No duplicate walls found");
            }
        }

        static void CheckDuplicateBeams(List<Core.Models.Elements.Beam> beams, Dictionary<string, string> levelLookup)
        {
            if (beams == null || beams.Count == 0)
            {
                Console.WriteLine("No beams to check for duplicates");
                return;
            }

            var duplicateGroups = new List<List<Core.Models.Elements.Beam>>();
            var processed = new HashSet<string>();

            for (int i = 0; i < beams.Count; i++)
            {
                if (processed.Contains(beams[i].Id)) continue;

                var duplicates = new List<Core.Models.Elements.Beam> { beams[i] };
                processed.Add(beams[i].Id);

                for (int j = i + 1; j < beams.Count; j++)
                {
                    if (processed.Contains(beams[j].Id)) continue;

                    if (AreBeamsGeometricallyEqual(beams[i], beams[j]))
                    {
                        duplicates.Add(beams[j]);
                        processed.Add(beams[j].Id);
                    }
                }

                if (duplicates.Count > 1)
                {
                    duplicateGroups.Add(duplicates);
                }
            }

            if (duplicateGroups.Count > 0)
            {
                Console.WriteLine($"DUPLICATE BEAMS FOUND: {duplicateGroups.Count} groups");
                for (int i = 0; i < duplicateGroups.Count; i++)
                {
                    var group = duplicateGroups[i];
                    Console.WriteLine($"  Group {i + 1}: {group.Count} duplicate beams on level {group[0].LevelId}");
                    foreach (var beam in group)
                    {
                        var start = beam.StartPoint;
                        var end = beam.EndPoint;
                        Console.WriteLine($"    Beam ID: {beam.Id} - From ({start?.X:F2},{start?.Y:F2}) to ({end?.X:F2},{end?.Y:F2})");
                    }
                }
            }
            else
            {
                Console.WriteLine("No duplicate beams found");
            }
        }

        static void CheckDuplicateColumns(List<Core.Models.Elements.Column> columns, Dictionary<string, string> levelLookup)
        {
            if (columns == null || columns.Count == 0)
            {
                Console.WriteLine("No columns to check for duplicates");
                return;
            }

            var duplicateGroups = new List<List<Core.Models.Elements.Column>>();
            var processed = new HashSet<string>();

            for (int i = 0; i < columns.Count; i++)
            {
                if (processed.Contains(columns[i].Id)) continue;

                var duplicates = new List<Core.Models.Elements.Column> { columns[i] };
                processed.Add(columns[i].Id);

                for (int j = i + 1; j < columns.Count; j++)
                {
                    if (processed.Contains(columns[j].Id)) continue;

                    if (AreColumnsGeometricallyEqual(columns[i], columns[j]))
                    {
                        duplicates.Add(columns[j]);
                        processed.Add(columns[j].Id);
                    }
                }

                if (duplicates.Count > 1)
                {
                    duplicateGroups.Add(duplicates);
                }
            }

            if (duplicateGroups.Count > 0)
            {
                Console.WriteLine($"DUPLICATE COLUMNS FOUND: {duplicateGroups.Count} groups");
                for (int i = 0; i < duplicateGroups.Count; i++)
                {
                    var group = duplicateGroups[i];
                    Console.WriteLine($"  Group {i + 1}: {group.Count} duplicate columns between levels {group[0].BaseLevelId} and {group[0].TopLevelId}");
                    foreach (var column in group)
                    {
                        var start = column.StartPoint;
                        Console.WriteLine($"    Column ID: {column.Id} - At ({start?.X:F2},{start?.Y:F2})");
                    }
                }
            }
            else
            {
                Console.WriteLine("No duplicate columns found");
            }
        }

        static void CheckDuplicateBraces(List<Core.Models.Elements.Brace> braces, Dictionary<string, string> levelLookup)
        {
            if (braces == null || braces.Count == 0)
            {
                Console.WriteLine("No braces to check for duplicates");
                return;
            }

            var duplicateGroups = new List<List<Core.Models.Elements.Brace>>();
            var processed = new HashSet<string>();

            for (int i = 0; i < braces.Count; i++)
            {
                if (processed.Contains(braces[i].Id)) continue;

                var duplicates = new List<Core.Models.Elements.Brace> { braces[i] };
                processed.Add(braces[i].Id);

                for (int j = i + 1; j < braces.Count; j++)
                {
                    if (processed.Contains(braces[j].Id)) continue;

                    if (AreBracesGeometricallyEqual(braces[i], braces[j]))
                    {
                        duplicates.Add(braces[j]);
                        processed.Add(braces[j].Id);
                    }
                }

                if (duplicates.Count > 1)
                {
                    duplicateGroups.Add(duplicates);
                }
            }

            if (duplicateGroups.Count > 0)
            {
                Console.WriteLine($"DUPLICATE BRACES FOUND: {duplicateGroups.Count} groups");
                for (int i = 0; i < duplicateGroups.Count; i++)
                {
                    var group = duplicateGroups[i];
                    Console.WriteLine($"  Group {i + 1}: {group.Count} duplicate braces between levels {group[0].BaseLevelId} and {group[0].TopLevelId}");
                    foreach (var brace in group)
                    {
                        var start = brace.StartPoint;
                        var end = brace.EndPoint;
                        Console.WriteLine($"    Brace ID: {brace.Id} - From ({start?.X:F2},{start?.Y:F2}) to ({end?.X:F2},{end?.Y:F2})");
                    }
                }
            }
            else
            {
                Console.WriteLine("No duplicate braces found");
            }
        }

        // Geometric comparison methods
        static bool AreFloorsGeometricallyEqual(Core.Models.Elements.Floor floor1, Core.Models.Elements.Floor floor2)
        {
            // Check level ID match
            if (floor1.LevelId != floor2.LevelId) return false;

            // Check if both have same number of points
            if ((floor1.Points?.Count ?? 0) != (floor2.Points?.Count ?? 0)) return false;

            // If no points, they're equal if levels match
            if ((floor1.Points?.Count ?? 0) == 0) return true;

            // Check if all points match (order matters for floors)
            for (int i = 0; i < floor1.Points.Count; i++)
            {
                if (!ArePointsEqual(floor1.Points[i], floor2.Points[i]))
                    return false;
            }

            return true;
        }

        static bool AreWallsGeometricallyEqual(Core.Models.Elements.Wall wall1, Core.Models.Elements.Wall wall2)
        {
            // Check level IDs match
            if (wall1.BaseLevelId != wall2.BaseLevelId || wall1.TopLevelId != wall2.TopLevelId) return false;

            // Check if both have same number of points
            if ((wall1.Points?.Count ?? 0) != (wall2.Points?.Count ?? 0)) return false;

            // If no points, they're equal if levels match
            if ((wall1.Points?.Count ?? 0) == 0) return true;

            // For walls, check both forward and reverse order (walls can be drawn in either direction)
            bool forwardMatch = true;
            bool reverseMatch = true;

            // Check forward order
            for (int i = 0; i < wall1.Points.Count; i++)
            {
                if (!ArePointsEqual(wall1.Points[i], wall2.Points[i]))
                {
                    forwardMatch = false;
                    break;
                }
            }

            // Check reverse order
            for (int i = 0; i < wall1.Points.Count; i++)
            {
                if (!ArePointsEqual(wall1.Points[i], wall2.Points[wall2.Points.Count - 1 - i]))
                {
                    reverseMatch = false;
                    break;
                }
            }

            return forwardMatch || reverseMatch;
        }

        static bool AreBeamsGeometricallyEqual(Core.Models.Elements.Beam beam1, Core.Models.Elements.Beam beam2)
        {
            // Check level ID match
            if (beam1.LevelId != beam2.LevelId) return false;

            // Check if both start/end points match (either direction)
            bool forwardMatch = ArePointsEqual(beam1.StartPoint, beam2.StartPoint) && ArePointsEqual(beam1.EndPoint, beam2.EndPoint);
            bool reverseMatch = ArePointsEqual(beam1.StartPoint, beam2.EndPoint) && ArePointsEqual(beam1.EndPoint, beam2.StartPoint);

            return forwardMatch || reverseMatch;
        }

        static bool AreColumnsGeometricallyEqual(Core.Models.Elements.Column column1, Core.Models.Elements.Column column2)
        {
            // Check level IDs match
            if (column1.BaseLevelId != column2.BaseLevelId || column1.TopLevelId != column2.TopLevelId) return false;

            // Check if start points match (columns are typically at the same location)
            return ArePointsEqual(column1.StartPoint, column2.StartPoint);
        }

        static bool AreBracesGeometricallyEqual(Core.Models.Elements.Brace brace1, Core.Models.Elements.Brace brace2)
        {
            // Check level IDs match
            if (brace1.BaseLevelId != brace2.BaseLevelId || brace1.TopLevelId != brace2.TopLevelId) return false;

            // Check if both start/end points match (either direction)
            bool forwardMatch = ArePointsEqual(brace1.StartPoint, brace2.StartPoint) && ArePointsEqual(brace1.EndPoint, brace2.EndPoint);
            bool reverseMatch = ArePointsEqual(brace1.StartPoint, brace2.EndPoint) && ArePointsEqual(brace1.EndPoint, brace2.StartPoint);

            return forwardMatch || reverseMatch;
        }

        static bool ArePointsEqual(Core.Models.Geometry.Point2D point1, Core.Models.Geometry.Point2D point2)
        {
            if (point1 == null && point2 == null) return true;
            if (point1 == null || point2 == null) return false;

            return Math.Abs(point1.X - point2.X) < CoordinateTolerance &&
                   Math.Abs(point1.Y - point2.Y) < CoordinateTolerance;
        }

        static void AnalyzeFloorTypes(BaseModel model)
        {
            var floorTypes = model.ModelLayout?.FloorTypes;

            Console.WriteLine("=== FLOOR TYPES ===");
            if (floorTypes == null || floorTypes.Count == 0)
            {
                Console.WriteLine("No floor types found");
                return;
            }

            Console.WriteLine($"Found {floorTypes.Count} floor types:");
            Console.WriteLine("{0,-10} {1,-30} {2,-40}", "Index", "Name", "ID");
            Console.WriteLine(new string('-', 80));

            for (int i = 0; i < floorTypes.Count; i++)
            {
                var floorType = floorTypes[i];
                Console.WriteLine("{0,-10} {1,-30} {2,-40}", i, floorType.Name, floorType.Id);
            }
            Console.WriteLine();
        }

        static void AnalyzeLevels(BaseModel model)
        {
            var levels = model.ModelLayout?.Levels;
            var floorTypes = model.ModelLayout?.FloorTypes;

            Console.WriteLine("=== LEVELS ===");
            if (levels == null || levels.Count == 0)
            {
                Console.WriteLine("No levels found");
                return;
            }

            // Create a lookup for floor types by ID
            Dictionary<string, string> floorTypeNames = new Dictionary<string, string>();
            if (floorTypes != null)
            {
                foreach (var floorType in floorTypes)
                {
                    floorTypeNames[floorType.Id] = floorType.Name;
                }
            }

            Console.WriteLine($"Found {levels.Count} levels (sorted by elevation):");
            Console.WriteLine("{0,-10} {1,-20} {2,-15} {3,-40} {4,-40}", "Index", "Name", "Elevation", "Floor Type", "Level ID");
            Console.WriteLine(new string('-', 125));

            // Sort levels by elevation for a better view
            var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

            for (int i = 0; i < sortedLevels.Count; i++)
            {
                var level = sortedLevels[i];
                string floorTypeInfo = "N/A";

                if (!string.IsNullOrEmpty(level.FloorTypeId) && floorTypeNames.ContainsKey(level.FloorTypeId))
                {
                    floorTypeInfo = $"{floorTypeNames[level.FloorTypeId]} (ID: {level.FloorTypeId})";
                }
                else if (!string.IsNullOrEmpty(level.FloorTypeId))
                {
                    floorTypeInfo = level.FloorTypeId;
                }

                Console.WriteLine("{0,-10} {1,-20} {2,-15} {3,-40} {4,-40}",
                    i, level.Name, level.Elevation, floorTypeInfo, level.Id);
            }
            Console.WriteLine();
        }

        static void AnalyzeBeams(BaseModel model)
        {
            var beams = model.Elements?.Beams;
            var levels = model.ModelLayout?.Levels;
            var floorTypes = model.ModelLayout?.FloorTypes;

            Console.WriteLine("=== BEAMS ===");
            if (beams == null || beams.Count == 0)
            {
                Console.WriteLine("No beams found");
                return;
            }

            // Create a lookup for levels by ID
            Dictionary<string, Level> levelsById = new Dictionary<string, Level>();
            if (levels != null)
            {
                foreach (var level in levels)
                {
                    levelsById[level.Id] = level;
                }
            }

            // Create a lookup for floor types by ID
            Dictionary<string, string> floorTypeNames = new Dictionary<string, string>();
            if (floorTypes != null)
            {
                foreach (var floorType in floorTypes)
                {
                    floorTypeNames[floorType.Id] = floorType.Name;
                }
            }

            // Group beams by level
            var beamsByLevel = beams.GroupBy(b => b.LevelId).ToList();

            Console.WriteLine($"Found {beams.Count} beams across {beamsByLevel.Count} levels:");
            Console.WriteLine();

            foreach (var group in beamsByLevel.OrderBy(g =>
                levelsById.ContainsKey(g.Key) ? levelsById[g.Key].Elevation : double.MaxValue))
            {
                string levelName = "Unknown Level";
                string floorTypeInfo = "Unknown";

                if (levelsById.ContainsKey(group.Key))
                {
                    var level = levelsById[group.Key];
                    levelName = level.Name;
                    string floorTypeId = level.FloorTypeId ?? "N/A";

                    if (!string.IsNullOrEmpty(floorTypeId) && floorTypeNames.ContainsKey(floorTypeId))
                    {
                        floorTypeInfo = $"{floorTypeNames[floorTypeId]} (ID: {floorTypeId})";
                    }
                    else
                    {
                        floorTypeInfo = floorTypeId;
                    }
                }

                Console.WriteLine($"Level: {levelName} (ID: {group.Key}) - Floor Type: {floorTypeInfo}");
                Console.WriteLine($"  Beams: {group.Count()}");

                // Sample a few beams for inspection
                int sampleSize = Math.Min(3, group.Count());
                Console.WriteLine($"  Sample of {sampleSize} beams:");

                foreach (var beam in group.Take(sampleSize))
                {
                    string coords = "N/A";
                    if (beam.StartPoint != null && beam.EndPoint != null)
                    {
                        coords = $"({beam.StartPoint.X},{beam.StartPoint.Y}) to ({beam.EndPoint.X},{beam.EndPoint.Y})";
                    }

                    Console.WriteLine($"    Beam ID: {beam.Id} - Props: {beam.FramePropertiesId} - Coords: {coords}");
                }
                Console.WriteLine();
            }
        }

        static void AnalyzeColumns(BaseModel model)
        {
            var columns = model.Elements?.Columns;
            var levels = model.ModelLayout?.Levels;
            var floorTypes = model.ModelLayout?.FloorTypes;

            Console.WriteLine("=== COLUMNS ===");
            if (columns == null || columns.Count == 0)
            {
                Console.WriteLine("No columns found");
                return;
            }

            // Create a lookup for levels by ID
            Dictionary<string, Level> levelsById = new Dictionary<string, Level>();
            if (levels != null)
            {
                foreach (var level in levels)
                {
                    levelsById[level.Id] = level;
                }
            }

            // Create a lookup for floor types by ID
            Dictionary<string, string> floorTypeNames = new Dictionary<string, string>();
            if (floorTypes != null)
            {
                foreach (var floorType in floorTypes)
                {
                    floorTypeNames[floorType.Id] = floorType.Name;
                }
            }

            // Group columns by top level
            var columnsByLevel = columns.GroupBy(c => c.TopLevelId).ToList();

            Console.WriteLine($"Found {columns.Count} columns across {columnsByLevel.Count} levels:");
            Console.WriteLine();

            foreach (var group in columnsByLevel.OrderBy(g =>
                levelsById.ContainsKey(g.Key) ? levelsById[g.Key].Elevation : double.MaxValue))
            {
                string levelName = "Unknown Level";
                string floorTypeInfo = "Unknown";

                if (levelsById.ContainsKey(group.Key))
                {
                    var level = levelsById[group.Key];
                    levelName = level.Name;
                    string floorTypeId = level.FloorTypeId ?? "N/A";

                    if (!string.IsNullOrEmpty(floorTypeId) && floorTypeNames.ContainsKey(floorTypeId))
                    {
                        floorTypeInfo = $"{floorTypeNames[floorTypeId]} (ID: {floorTypeId})";
                    }
                    else
                    {
                        floorTypeInfo = floorTypeId;
                    }
                }

                Console.WriteLine($"Top Level: {levelName} (ID: {group.Key}) - Floor Type: {floorTypeInfo}");
                Console.WriteLine($"  Columns: {group.Count()}");

                // Sample a few columns for inspection
                int sampleSize = Math.Min(3, group.Count());
                Console.WriteLine($"  Sample of {sampleSize} columns:");

                foreach (var column in group.Take(sampleSize))
                {
                    string coords = "N/A";
                    if (column.StartPoint != null)
                    {
                        coords = $"({column.StartPoint.X},{column.StartPoint.Y})";
                        if (column.EndPoint != null)
                        {
                            coords += $" to ({column.EndPoint.X},{column.EndPoint.Y})";
                        }
                    }

                    Console.WriteLine($"    Column ID: {column.Id} - Props: {column.FramePropertiesId} - Base Level: {column.BaseLevelId} - Coords: {coords}");
                }
                Console.WriteLine();
            }
        }

        static void AnalyzeBraces(BaseModel model)
        {
            var braces = model.Elements?.Braces;
            var levels = model.ModelLayout?.Levels;
            var floorTypes = model.ModelLayout?.FloorTypes;

            Console.WriteLine("=== BRACES ===");
            if (braces == null || braces.Count == 0)
            {
                Console.WriteLine("No braces found");
                return;
            }

            // Create a lookup for levels by ID
            Dictionary<string, Level> levelsById = new Dictionary<string, Level>();
            if (levels != null)
            {
                foreach (var level in levels)
                {
                    levelsById[level.Id] = level;
                }
            }

            // Create a lookup for floor types by ID
            Dictionary<string, string> floorTypeNames = new Dictionary<string, string>();
            if (floorTypes != null)
            {
                foreach (var floorType in floorTypes)
                {
                    floorTypeNames[floorType.Id] = floorType.Name;
                }
            }

            // Group braces by top level
            var bracesByLevel = braces.GroupBy(b => b.TopLevelId).ToList();

            Console.WriteLine($"Found {braces.Count} braces across {bracesByLevel.Count} levels:");
            Console.WriteLine();

            foreach (var group in bracesByLevel.OrderBy(g =>
                levelsById.ContainsKey(g.Key) ? levelsById[g.Key].Elevation : double.MaxValue))
            {
                string levelName = "Unknown Level";
                string floorTypeInfo = "Unknown";

                if (levelsById.ContainsKey(group.Key))
                {
                    var level = levelsById[group.Key];
                    levelName = level.Name;
                    string floorTypeId = level.FloorTypeId ?? "N/A";

                    if (!string.IsNullOrEmpty(floorTypeId) && floorTypeNames.ContainsKey(floorTypeId))
                    {
                        floorTypeInfo = $"{floorTypeNames[floorTypeId]} (ID: {floorTypeId})";
                    }
                    else
                    {
                        floorTypeInfo = floorTypeId;
                    }
                }

                Console.WriteLine($"Top Level: {levelName} (ID: {group.Key}) - Floor Type: {floorTypeInfo}");
                Console.WriteLine($"  Braces: {group.Count()}");

                // Sample a few braces for inspection
                int sampleSize = Math.Min(3, group.Count());
                Console.WriteLine($"  Sample of {sampleSize} braces:");

                foreach (var brace in group.Take(sampleSize))
                {
                    string coords = "N/A";
                    if (brace.StartPoint != null && brace.EndPoint != null)
                    {
                        coords = $"({brace.StartPoint.X},{brace.StartPoint.Y}) to ({brace.EndPoint.X},{brace.EndPoint.Y})";
                    }

                    string baseLevelName = "N/A";
                    if (!string.IsNullOrEmpty(brace.BaseLevelId) && levelsById.ContainsKey(brace.BaseLevelId))
                    {
                        baseLevelName = levelsById[brace.BaseLevelId].Name;
                    }

                    Console.WriteLine($"    Brace ID: {brace.Id} - Props: {brace.FramePropertiesId} - Base Level: {baseLevelName} - Coords: {coords}");
                }
                Console.WriteLine();
            }
        }

        static void AnalyzeFrameProperties(BaseModel model)
        {
            Console.WriteLine("=== FRAME PROPERTIES ANALYSIS ===");

            var frameProperties = model.Properties?.FrameProperties;
            var materials = model.Properties?.Materials;
            var columns = model.Elements?.Columns;
            var beams = model.Elements?.Beams;
            var braces = model.Elements?.Braces;

            if (frameProperties == null || frameProperties.Count == 0)
            {
                Console.WriteLine("No frame properties found");
                return;
            }

            // Create material lookup
            Dictionary<string, string> materialNames = new Dictionary<string, string>();
            if (materials != null)
            {
                foreach (var material in materials)
                {
                    materialNames[material.Id] = material.Name;
                }
            }

            Console.WriteLine($"Found {frameProperties.Count} frame properties:");
            Console.WriteLine("{0,-40} {1,-15} {2,-40} {3,-40}", "Name", "Type", "Material", "Property ID");
            Console.WriteLine(new string('-', 135));

            foreach (var frameProp in frameProperties)
            {
                string materialInfo = "N/A";
                if (!string.IsNullOrEmpty(frameProp.MaterialId) && materialNames.ContainsKey(frameProp.MaterialId))
                {
                    materialInfo = $"{materialNames[frameProp.MaterialId]} (ID: {frameProp.MaterialId})";
                }
                else if (!string.IsNullOrEmpty(frameProp.MaterialId))
                {
                    materialInfo = $"Missing Material (ID: {frameProp.MaterialId})";
                }

                Console.WriteLine("{0,-40} {1,-15} {2,-40} {3,-40}",
                    frameProp.Name ?? "N/A", frameProp.Type, materialInfo, frameProp.Id);
            }
            Console.WriteLine();

            // Analyze columns by frame properties
            AnalyzeColumnsByFrameProperties(columns, frameProperties, materialNames);

            // Analyze beams by frame properties
            AnalyzeBeamsByFrameProperties(beams, frameProperties, materialNames);

            // Analyze braces by frame properties
            AnalyzeBracesByFrameProperties(braces, frameProperties, materialNames);
        }

        static void AnalyzeColumnsByFrameProperties(List<Core.Models.Elements.Column> columns,
            List<Core.Models.Properties.FrameProperties> frameProperties,
            Dictionary<string, string> materialNames)
        {
            Console.WriteLine("=== COLUMNS BY FRAME PROPERTIES ===");

            if (columns == null || columns.Count == 0)
            {
                Console.WriteLine("No columns found");
                return;
            }

            // Create frame properties lookup
            Dictionary<string, Core.Models.Properties.FrameProperties> framePropsById =
                new Dictionary<string, Core.Models.Properties.FrameProperties>();
            if (frameProperties != null)
            {
                foreach (var frameProp in frameProperties)
                {
                    framePropsById[frameProp.Id] = frameProp;
                }
            }

            // Group columns by frame properties
            var columnsByFrameProps = columns.GroupBy(c => c.FramePropertiesId).ToList();

            Console.WriteLine($"Found {columns.Count} columns using {columnsByFrameProps.Count} different frame properties:");
            Console.WriteLine();

            foreach (var group in columnsByFrameProps.OrderBy(g =>
                framePropsById.ContainsKey(g.Key ?? "") ? framePropsById[g.Key].Name : "ZZZ_Missing"))
            {
                string propName = "Missing Property";
                string materialInfo = "N/A";
                bool isMissing = false;

                if (!string.IsNullOrEmpty(group.Key) && framePropsById.ContainsKey(group.Key))
                {
                    var frameProp = framePropsById[group.Key];
                    propName = frameProp.Name ?? "Unnamed Property";

                    if (!string.IsNullOrEmpty(frameProp.MaterialId) && materialNames.ContainsKey(frameProp.MaterialId))
                    {
                        materialInfo = $"{materialNames[frameProp.MaterialId]} (ID: {frameProp.MaterialId})";
                    }
                    else if (!string.IsNullOrEmpty(frameProp.MaterialId))
                    {
                        materialInfo = $"Missing Material (ID: {frameProp.MaterialId})";
                    }
                }
                else
                {
                    isMissing = true;
                    propName = $"MISSING PROPERTY (ID: {group.Key ?? "null"})";
                }

                string warningFlag = isMissing ? " ⚠️" : "";
                Console.WriteLine($"Frame Property: {propName}{warningFlag}");
                Console.WriteLine($"  Material: {materialInfo}");
                Console.WriteLine($"  Property ID: {group.Key ?? "null"}");
                Console.WriteLine($"  Columns using this property: {group.Count()}");

                // Sample a few columns
                int sampleSize = Math.Min(3, group.Count());
                Console.WriteLine($"  Sample of {sampleSize} columns:");

                foreach (var column in group.Take(sampleSize))
                {
                    string coords = "N/A";
                    if (column.StartPoint != null)
                    {
                        coords = $"({column.StartPoint.X:F1},{column.StartPoint.Y:F1})";
                    }

                    Console.WriteLine($"    Column ID: {column.Id} - Location: {coords} - Base: {column.BaseLevelId} - Top: {column.TopLevelId}");
                }
                Console.WriteLine();
            }
        }

        static void AnalyzeBeamsByFrameProperties(List<Core.Models.Elements.Beam> beams,
            List<Core.Models.Properties.FrameProperties> frameProperties,
            Dictionary<string, string> materialNames)
        {
            Console.WriteLine("=== BEAMS BY FRAME PROPERTIES ===");

            if (beams == null || beams.Count == 0)
            {
                Console.WriteLine("No beams found");
                return;
            }

            // Create frame properties lookup
            Dictionary<string, Core.Models.Properties.FrameProperties> framePropsById =
                new Dictionary<string, Core.Models.Properties.FrameProperties>();
            if (frameProperties != null)
            {
                foreach (var frameProp in frameProperties)
                {
                    framePropsById[frameProp.Id] = frameProp;
                }
            }

            // Group beams by frame properties
            var beamsByFrameProps = beams.GroupBy(b => b.FramePropertiesId).ToList();

            Console.WriteLine($"Found {beams.Count} beams using {beamsByFrameProps.Count} different frame properties:");
            Console.WriteLine();

            foreach (var group in beamsByFrameProps.OrderBy(g =>
                framePropsById.ContainsKey(g.Key ?? "") ? framePropsById[g.Key].Name : "ZZZ_Missing"))
            {
                string propName = "Missing Property";
                string materialInfo = "N/A";
                bool isMissing = false;

                if (!string.IsNullOrEmpty(group.Key) && framePropsById.ContainsKey(group.Key))
                {
                    var frameProp = framePropsById[group.Key];
                    propName = frameProp.Name ?? "Unnamed Property";

                    if (!string.IsNullOrEmpty(frameProp.MaterialId) && materialNames.ContainsKey(frameProp.MaterialId))
                    {
                        materialInfo = $"{materialNames[frameProp.MaterialId]} (ID: {frameProp.MaterialId})";
                    }
                    else if (!string.IsNullOrEmpty(frameProp.MaterialId))
                    {
                        materialInfo = $"Missing Material (ID: {frameProp.MaterialId})";
                    }
                }
                else
                {
                    isMissing = true;
                    propName = $"MISSING PROPERTY (ID: {group.Key ?? "null"})";
                }

                string warningFlag = isMissing ? " ⚠️" : "";
                Console.WriteLine($"Frame Property: {propName}{warningFlag}");
                Console.WriteLine($"  Material: {materialInfo}");
                Console.WriteLine($"  Property ID: {group.Key ?? "null"}");
                Console.WriteLine($"  Beams using this property: {group.Count()}");

                // Sample a few beams
                int sampleSize = Math.Min(3, group.Count());
                Console.WriteLine($"  Sample of {sampleSize} beams:");

                foreach (var beam in group.Take(sampleSize))
                {
                    string coords = "N/A";
                    if (beam.StartPoint != null && beam.EndPoint != null)
                    {
                        coords = $"({beam.StartPoint.X:F1},{beam.StartPoint.Y:F1}) to ({beam.EndPoint.X:F1},{beam.EndPoint.Y:F1})";
                    }

                    Console.WriteLine($"    Beam ID: {beam.Id} - Level: {beam.LevelId} - Coords: {coords}");
                }
                Console.WriteLine();
            }
        }

        static void AnalyzeBracesByFrameProperties(List<Core.Models.Elements.Brace> braces,
            List<Core.Models.Properties.FrameProperties> frameProperties,
            Dictionary<string, string> materialNames)
        {
            Console.WriteLine("=== BRACES BY FRAME PROPERTIES ===");

            if (braces == null || braces.Count == 0)
            {
                Console.WriteLine("No braces found");
                return;
            }

            // Create frame properties lookup
            Dictionary<string, Core.Models.Properties.FrameProperties> framePropsById =
                new Dictionary<string, Core.Models.Properties.FrameProperties>();
            if (frameProperties != null)
            {
                foreach (var frameProp in frameProperties)
                {
                    framePropsById[frameProp.Id] = frameProp;
                }
            }

            // Group braces by frame properties
            var bracesByFrameProps = braces.GroupBy(b => b.FramePropertiesId).ToList();

            Console.WriteLine($"Found {braces.Count} braces using {bracesByFrameProps.Count} different frame properties:");
            Console.WriteLine();

            foreach (var group in bracesByFrameProps.OrderBy(g =>
                framePropsById.ContainsKey(g.Key ?? "") ? framePropsById[g.Key].Name : "ZZZ_Missing"))
            {
                string propName = "Missing Property";
                string materialInfo = "N/A";
                bool isMissing = false;

                if (!string.IsNullOrEmpty(group.Key) && framePropsById.ContainsKey(group.Key))
                {
                    var frameProp = framePropsById[group.Key];
                    propName = frameProp.Name ?? "Unnamed Property";

                    if (!string.IsNullOrEmpty(frameProp.MaterialId) && materialNames.ContainsKey(frameProp.MaterialId))
                    {
                        materialInfo = $"{materialNames[frameProp.MaterialId]} (ID: {frameProp.MaterialId})";
                    }
                    else if (!string.IsNullOrEmpty(frameProp.MaterialId))
                    {
                        materialInfo = $"Missing Material (ID: {frameProp.MaterialId})";
                    }
                }
                else
                {
                    isMissing = true;
                    propName = $"MISSING PROPERTY (ID: {group.Key ?? "null"})";
                }

                string warningFlag = isMissing ? " ⚠️" : "";
                Console.WriteLine($"Frame Property: {propName}{warningFlag}");
                Console.WriteLine($"  Material: {materialInfo}");
                Console.WriteLine($"  Property ID: {group.Key ?? "null"}");
                Console.WriteLine($"  Braces using this property: {group.Count()}");

                // Sample a few braces
                int sampleSize = Math.Min(3, group.Count());
                Console.WriteLine($"  Sample of {sampleSize} braces:");

                foreach (var brace in group.Take(sampleSize))
                {
                    string coords = "N/A";
                    if (brace.StartPoint != null && brace.EndPoint != null)
                    {
                        coords = $"({brace.StartPoint.X:F1},{brace.StartPoint.Y:F1}) to ({brace.EndPoint.X:F1},{brace.EndPoint.Y:F1})";
                    }

                    Console.WriteLine($"    Brace ID: {brace.Id} - Base: {brace.BaseLevelId} - Top: {brace.TopLevelId} - Coords: {coords}");
                }
                Console.WriteLine();
            }
        }
    }
}
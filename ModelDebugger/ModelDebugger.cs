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

            // Analyze Beams and their relationships
            AnalyzeBeams(model);

            // Analyze Columns and their relationships
            AnalyzeColumns(model);

            // Analyze Braces and their relationships
            AnalyzeBraces(model);

            AnalyzeWalls(model);

            AnalyzeFloors(model);
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

        static void AnalyzeWalls(BaseModel model)
        {
            var walls = model.Elements?.Walls;
            var levels = model.ModelLayout?.Levels;
            var floorTypes = model.ModelLayout?.FloorTypes;
            var wallProperties = model.Properties?.WallProperties;

            Console.WriteLine("=== WALLS ===");
            if (walls == null || walls.Count == 0)
            {
                Console.WriteLine("No walls found");
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

            // Create a lookup for wall properties by ID
            Dictionary<string, string> wallPropsNames = new Dictionary<string, string>();
            if (wallProperties != null)
            {
                foreach (var prop in wallProperties)
                {
                    wallPropsNames[prop.Id] = $"{prop.Name} ({prop.Thickness}\")";
                }
            }

            // Group walls by top level
            var wallsByLevel = walls.GroupBy(w => w.TopLevelId).ToList();

            Console.WriteLine($"Found {walls.Count} walls across {wallsByLevel.Count} levels:");
            Console.WriteLine();

            foreach (var group in wallsByLevel.OrderBy(g =>
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
                Console.WriteLine($"  Walls: {group.Count()}");

                // Sample a few walls for inspection
                int sampleSize = Math.Min(3, group.Count());
                Console.WriteLine($"  Sample of {sampleSize} walls:");

                foreach (var wall in group.Take(sampleSize))
                {
                    string coords = "N/A";
                    if (wall.Points != null && wall.Points.Count >= 2)
                    {
                        var firstPoint = wall.Points[0];
                        var lastPoint = wall.Points[wall.Points.Count - 1];
                        coords = $"({firstPoint.X:F1},{firstPoint.Y:F1}) to ({lastPoint.X:F1},{lastPoint.Y:F1})";
                        if (wall.Points.Count > 2)
                        {
                            coords += $" ({wall.Points.Count} points)";
                        }
                    }

                    string baseLevelName = "N/A";
                    if (!string.IsNullOrEmpty(wall.BaseLevelId) && levelsById.ContainsKey(wall.BaseLevelId))
                    {
                        baseLevelName = levelsById[wall.BaseLevelId].Name;
                    }

                    string wallPropsInfo = "N/A";
                    if (!string.IsNullOrEmpty(wall.PropertiesId) && wallPropsNames.ContainsKey(wall.PropertiesId))
                    {
                        wallPropsInfo = wallPropsNames[wall.PropertiesId];
                    }

                    Console.WriteLine($"    Wall ID: {wall.Id} - Props: {wallPropsInfo} - Base Level: {baseLevelName} - Coords: {coords}");
                }
                Console.WriteLine();
            }
        }

        static void AnalyzeFloors(BaseModel model)
        {
            var floors = model.Elements?.Floors;
            var levels = model.ModelLayout?.Levels;
            var floorTypes = model.ModelLayout?.FloorTypes;
            var floorProperties = model.Properties?.FloorProperties;

            Console.WriteLine("=== FLOORS ===");
            if (floors == null || floors.Count == 0)
            {
                Console.WriteLine("No floors found");
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

            // Create a lookup for floor properties by ID
            Dictionary<string, string> floorPropsNames = new Dictionary<string, string>();
            if (floorProperties != null)
            {
                foreach (var prop in floorProperties)
                {
                    floorPropsNames[prop.Id] = $"{prop.Name} ({prop.Thickness}\")";
                }
            }

            // Group floors by level
            var floorsByLevel = floors.GroupBy(f => f.LevelId).ToList();

            Console.WriteLine($"Found {floors.Count} floors across {floorsByLevel.Count} levels:");
            Console.WriteLine();

            foreach (var group in floorsByLevel.OrderBy(g =>
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
                Console.WriteLine($"  Floors: {group.Count()}");

                // Sample a few floors for inspection
                int sampleSize = Math.Min(3, group.Count());
                Console.WriteLine($"  Sample of {sampleSize} floors:");

                foreach (var floor in group.Take(sampleSize))
                {
                    string coords = "N/A";
                    if (floor.Points != null && floor.Points.Count >= 3)
                    {
                        var firstPoint = floor.Points[0];
                        coords = $"Starting at ({firstPoint.X:F1},{firstPoint.Y:F1}) - {floor.Points.Count} points";
                    }

                    string floorPropsInfo = "N/A";
                    if (!string.IsNullOrEmpty(floor.FloorPropertiesId) && floorPropsNames.ContainsKey(floor.FloorPropertiesId))
                    {
                        floorPropsInfo = floorPropsNames[floor.FloorPropertiesId];
                    }

                    string diaphragmInfo = string.IsNullOrEmpty(floor.DiaphragmId) ? "None" : floor.DiaphragmId;
                    string loadInfo = string.IsNullOrEmpty(floor.SurfaceLoadId) ? "None" : floor.SurfaceLoadId;

                    Console.WriteLine($"    Floor ID: {floor.Id} - Props: {floorPropsInfo}");
                    Console.WriteLine($"           Diaphragm: {diaphragmInfo} - Load: {loadInfo} - Coords: {coords}");
                }
                Console.WriteLine();
            }
        }
    }
}
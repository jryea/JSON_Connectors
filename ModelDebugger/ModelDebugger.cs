using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

namespace ModelDebugger
{
    internal class ModelDebugger
    {
        internal static void RunModelDebugger(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide the path to your JSON file.");
                Console.WriteLine("Usage: ModelDebugger.exe path/to/model.json");
                return;
            }

            string jsonFilePath = args[0];

            try
            {
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
            Console.WriteLine("{0,-10} {1,-20} {2,-15} {3,-30} {4,-40}", "Index", "Name", "Elevation", "Floor Type", "Level ID");
            Console.WriteLine(new string('-', 115));

            // Sort levels by elevation for a better view
            var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

            for (int i = 0; i < sortedLevels.Count; i++)
            {
                var level = sortedLevels[i];
                string floorTypeName = "N/A";

                if (!string.IsNullOrEmpty(level.FloorTypeId) && floorTypeNames.ContainsKey(level.FloorTypeId))
                {
                    floorTypeName = floorTypeNames[level.FloorTypeId];
                }

                Console.WriteLine("{0,-10} {1,-20} {2,-15} {3,-30} {4,-40}",
                    i, level.Name, level.Elevation, floorTypeName, level.Id);
            }
            Console.WriteLine();
        }

        static void AnalyzeBeams(BaseModel model)
        {
            var beams = model.Elements?.Beams;
            var levels = model.ModelLayout?.Levels;

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

            // Group beams by level
            var beamsByLevel = beams.GroupBy(b => b.LevelId).ToList();

            Console.WriteLine($"Found {beams.Count} beams across {beamsByLevel.Count} levels:");
            Console.WriteLine();

            foreach (var group in beamsByLevel.OrderBy(g =>
                levelsById.ContainsKey(g.Key) ? levelsById[g.Key].Elevation : double.MaxValue))
            {
                string levelName = "Unknown Level";
                string floorTypeId = "Unknown";

                if (levelsById.ContainsKey(group.Key))
                {
                    var level = levelsById[group.Key];
                    levelName = level.Name;
                    floorTypeId = level.FloorTypeId ?? "N/A";
                }

                Console.WriteLine($"Level: {levelName} (ID: {group.Key}) - Floor Type ID: {floorTypeId}");
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

            // Group columns by top level
            var columnsByLevel = columns.GroupBy(c => c.TopLevelId).ToList();

            Console.WriteLine($"Found {columns.Count} columns across {columnsByLevel.Count} levels:");
            Console.WriteLine();

            foreach (var group in columnsByLevel.OrderBy(g =>
                levelsById.ContainsKey(g.Key) ? levelsById[g.Key].Elevation : double.MaxValue))
            {
                string levelName = "Unknown Level";
                string floorTypeId = "Unknown";

                if (levelsById.ContainsKey(group.Key))
                {
                    var level = levelsById[group.Key];
                    levelName = level.Name;
                    floorTypeId = level.FloorTypeId ?? "N/A";
                }

                Console.WriteLine($"Top Level: {levelName} (ID: {group.Key}) - Floor Type ID: {floorTypeId}");
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

            // Group braces by top level
            var bracesByLevel = braces.GroupBy(b => b.TopLevelId).ToList();

            Console.WriteLine($"Found {braces.Count} braces across {bracesByLevel.Count} levels:");
            Console.WriteLine();

            foreach (var group in bracesByLevel.OrderBy(g =>
                levelsById.ContainsKey(g.Key) ? levelsById[g.Key].Elevation : double.MaxValue))
            {
                string levelName = "Unknown Level";
                string floorTypeId = "Unknown";

                if (levelsById.ContainsKey(group.Key))
                {
                    var level = levelsById[group.Key];
                    levelName = level.Name;
                    floorTypeId = level.FloorTypeId ?? "N/A";
                }

                Console.WriteLine($"Top Level: {levelName} (ID: {group.Key}) - Floor Type ID: {floorTypeId}");
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

        // Define minimal classes needed for deserialization
        public class BaseModel
        {
            public MetadataContainer Metadata { get; set; }
            public ModelLayoutContainer ModelLayout { get; set; }
            public ElementContainer Elements { get; set; }
        }

        public class MetadataContainer
        {
            public ProjectInfo ProjectInfo { get; set; }
            public Units Units { get; set; }
        }

        public class ProjectInfo
        {
            public string ProjectName { get; set; }
        }

        public class Units
        {
            public string Length { get; set; }
        }

        public class ModelLayoutContainer
        {
            public List<FloorType> FloorTypes { get; set; } = new List<FloorType>();
            public List<Level> Levels { get; set; } = new List<Level>();
        }

        public class FloorType
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        public class Level
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public double Elevation { get; set; }
            public string FloorTypeId { get; set; }
        }

        public class ElementContainer
        {
            public List<Beam> Beams { get; set; } = new List<Beam>();
            public List<Column> Columns { get; set; } = new List<Column>();
            public List<Brace> Braces { get; set; } = new List<Brace>();
        }

        public class Beam
        {
            public string Id { get; set; }
            public Point2D StartPoint { get; set; }
            public Point2D EndPoint { get; set; }
            public string LevelId { get; set; }
            public string FramePropertiesId { get; set; }
        }

        public class Column
        {
            public string Id { get; set; }
            public Point2D StartPoint { get; set; }
            public Point2D EndPoint { get; set; }
            public string BaseLevelId { get; set; }
            public string TopLevelId { get; set; }
            public string FramePropertiesId { get; set; }
        }

        public class Brace
        {
            public string Id { get; set; }
            public Point2D StartPoint { get; set; }
            public Point2D EndPoint { get; set; }
            public string BaseLevelId { get; set; }
            public string TopLevelId { get; set; }
            public string FramePropertiesId { get; set; }
        }

        public class Point2D
        {
            public double X { get; set; }
            public double Y { get; set; }
        }
    }
}

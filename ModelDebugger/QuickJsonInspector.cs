using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ModelDebugger
{
    internal class QuickJsonInspector
    {
        internal static void RunQuickInspector(string[] args)
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

            if (!File.Exists(jsonFilePath))
            {
                Console.WriteLine($"File not found: {jsonFilePath}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            try
            {
                Console.WriteLine($"Analyzing JSON file: {Path.GetFileName(jsonFilePath)}");
                string jsonContent = File.ReadAllText(jsonFilePath);
                using (JsonDocument document = JsonDocument.Parse(jsonContent))
                {
                    AnalyzeJsonDocument(document.RootElement);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing JSON: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static void AnalyzeJsonDocument(JsonElement root)
        {
            // Quickly explore key relationships
            Console.WriteLine("=== QUICK JSON ANALYSIS ===");

            // Analyze floor types
            JsonElement floorTypesElement = root.GetProperty("modelLayout").GetProperty("floorTypes");
            int floorTypeCount = floorTypesElement.GetArrayLength();
            Console.WriteLine($"\nFloor Types: {floorTypeCount} found");

            // Create mapping from floor type ID to name for later use
            Dictionary<string, string> floorTypeNames = new Dictionary<string, string>();

            // Print first few floor types as a sample
            for (int i = 0; i < Math.Min(5, floorTypeCount); i++)
            {
                var floorType = floorTypesElement[i];
                string id = floorType.GetProperty("id").GetString();
                string name = floorType.GetProperty("name").GetString();
                Console.WriteLine($"  {i}: {name} (ID: {id})");
                floorTypeNames[id] = name;
            }

            // Analyze levels
            JsonElement levelsElement = root.GetProperty("modelLayout").GetProperty("levels");
            int levelCount = levelsElement.GetArrayLength();
            Console.WriteLine($"\nLevels: {levelCount} found");

            // Create mapping from level ID to (name, floorTypeId) for later use
            Dictionary<string, (string Name, string FloorTypeId)> levelInfo = new Dictionary<string, (string, string)>();

            // Organize levels by floor type
            var levelsByFloorType = new Dictionary<string, List<JsonElement>>();

            for (int i = 0; i < levelCount; i++)
            {
                var level = levelsElement[i];
                string levelId = level.GetProperty("id").GetString();
                string levelName = level.GetProperty("name").GetString();
                string floorTypeId = "null";

                if (level.TryGetProperty("floorTypeId", out JsonElement floorTypeIdElement))
                {
                    floorTypeId = floorTypeIdElement.GetString() ?? "null";
                }

                // Store level info for later reference
                levelInfo[levelId] = (levelName, floorTypeId);

                if (!levelsByFloorType.ContainsKey(floorTypeId))
                {
                    levelsByFloorType[floorTypeId] = new List<JsonElement>();
                }

                levelsByFloorType[floorTypeId].Add(level);
            }

            // Print levels grouped by floor type
            Console.WriteLine("\nLevels by Floor Type:");
            foreach (var entry in levelsByFloorType)
            {
                string floorTypeName = floorTypeNames.ContainsKey(entry.Key) ? floorTypeNames[entry.Key] : "Unknown";
                Console.WriteLine($"  Floor Type: {floorTypeName} (ID: {entry.Key})");

                // Sort levels by elevation
                var sortedLevels = entry.Value
                    .OrderBy(l => l.GetProperty("elevation").GetDouble())
                    .ToList();

                foreach (var level in sortedLevels)
                {
                    Console.WriteLine($"    Level: {level.GetProperty("name").GetString()} (ID: {level.GetProperty("id").GetString()}) - Elevation: {level.GetProperty("elevation").GetDouble()}");
                }
            }

            // Check for beam-level relationships
            if (root.TryGetProperty("elements", out JsonElement elementsElement) &&
                elementsElement.TryGetProperty("beams", out JsonElement beamsElement))
            {
                AnalyzeBeams(beamsElement, levelInfo, floorTypeNames);
            }

            // Check for column-level relationships
            if (elementsElement.TryGetProperty("columns", out JsonElement columnsElement))
            {
                AnalyzeColumns(columnsElement, levelInfo, floorTypeNames);
            }

            // Similarly for braces
            if (elementsElement.TryGetProperty("braces", out JsonElement bracesElement))
            {
                AnalyzeBraces(bracesElement, levelInfo, floorTypeNames);
            }
        }

        private static void AnalyzeBeams(JsonElement beamsElement,
            Dictionary<string, (string Name, string FloorTypeId)> levelInfo,
            Dictionary<string, string> floorTypeNames)
        {
            int beamCount = beamsElement.GetArrayLength();

            // Collect all level IDs
            var allLevelIds = new HashSet<string>(levelInfo.Keys);

            // Check for beams with invalid level references
            var invalidBeams = new List<JsonElement>();
            for (int i = 0; i < beamCount; i++)
            {
                var beam = beamsElement[i];
                if (beam.TryGetProperty("levelId", out JsonElement levelIdElement))
                {
                    string levelId = levelIdElement.GetString();
                    if (string.IsNullOrEmpty(levelId) || !allLevelIds.Contains(levelId))
                    {
                        invalidBeams.Add(beam);
                    }
                }
                else
                {
                    invalidBeams.Add(beam);
                }
            }

            Console.WriteLine($"\nBeams: {beamCount} found, {invalidBeams.Count} with invalid level references");

            if (invalidBeams.Count > 0)
            {
                Console.WriteLine("\nSample of beams with invalid level references:");
                for (int i = 0; i < Math.Min(3, invalidBeams.Count); i++)
                {
                    Console.WriteLine($"  Beam ID: {invalidBeams[i].GetProperty("id").GetString()} - Level ID: {(invalidBeams[i].TryGetProperty("levelId", out JsonElement levelId) ? levelId.GetString() : "missing")}");
                }
            }

            // Find the most common levels for beams
            var levelCounts = new Dictionary<string, int>();
            for (int i = 0; i < beamCount; i++)
            {
                var beam = beamsElement[i];
                string levelId = "null";
                if (beam.TryGetProperty("levelId", out JsonElement levelIdElement))
                {
                    levelId = levelIdElement.GetString() ?? "null";
                }

                if (!levelCounts.ContainsKey(levelId))
                {
                    levelCounts[levelId] = 0;
                }
                levelCounts[levelId]++;
            }

            Console.WriteLine("\nMost common levels for beams:");
            foreach (var entry in levelCounts.OrderByDescending(kvp => kvp.Value).Take(5))
            {
                string levelName = "Unknown";
                string floorTypeName = "Unknown";
                string floorTypeId = "Unknown";

                if (levelInfo.TryGetValue(entry.Key, out var info))
                {
                    levelName = info.Name;
                    floorTypeId = info.FloorTypeId;

                    if (floorTypeNames.TryGetValue(floorTypeId, out string name))
                    {
                        floorTypeName = name;
                    }
                }

                Console.WriteLine($"  Level: {levelName} (ID: {entry.Key}) - Floor Type: {floorTypeName} (ID: {floorTypeId}) - Beam Count: {entry.Value}");
            }
        }

        private static void AnalyzeColumns(JsonElement columnsElement,
            Dictionary<string, (string Name, string FloorTypeId)> levelInfo,
            Dictionary<string, string> floorTypeNames)
        {
            int columnCount = columnsElement.GetArrayLength();
            Console.WriteLine($"\nColumns: {columnCount} found");

            // Find the most common top levels for columns
            var topLevelCounts = new Dictionary<string, int>();
            for (int i = 0; i < columnCount; i++)
            {
                var column = columnsElement[i];
                string topLevelId = "null";
                if (column.TryGetProperty("topLevelId", out JsonElement topLevelIdElement))
                {
                    topLevelId = topLevelIdElement.GetString() ?? "null";
                }

                if (!topLevelCounts.ContainsKey(topLevelId))
                {
                    topLevelCounts[topLevelId] = 0;
                }
                topLevelCounts[topLevelId]++;
            }

            Console.WriteLine("\nMost common top levels for columns:");
            foreach (var entry in topLevelCounts.OrderByDescending(kvp => kvp.Value).Take(5))
            {
                string levelName = "Unknown";
                string floorTypeName = "Unknown";
                string floorTypeId = "Unknown";

                if (levelInfo.TryGetValue(entry.Key, out var info))
                {
                    levelName = info.Name;
                    floorTypeId = info.FloorTypeId;

                    if (floorTypeNames.TryGetValue(floorTypeId, out string name))
                    {
                        floorTypeName = name;
                    }
                }

                Console.WriteLine($"  Level: {levelName} (ID: {entry.Key}) - Floor Type: {floorTypeName} (ID: {floorTypeId}) - Column Count: {entry.Value}");
            }
        }

        private static void AnalyzeBraces(JsonElement bracesElement,
            Dictionary<string, (string Name, string FloorTypeId)> levelInfo,
            Dictionary<string, string> floorTypeNames)
        {
            int braceCount = bracesElement.GetArrayLength();
            Console.WriteLine($"\nBraces: {braceCount} found");

            // Find the most common top levels for braces
            var topLevelCounts = new Dictionary<string, int>();
            for (int i = 0; i < braceCount; i++)
            {
                var brace = bracesElement[i];
                string topLevelId = "null";
                if (brace.TryGetProperty("topLevelId", out JsonElement topLevelIdElement))
                {
                    topLevelId = topLevelIdElement.GetString() ?? "null";
                }

                if (!topLevelCounts.ContainsKey(topLevelId))
                {
                    topLevelCounts[topLevelId] = 0;
                }
                topLevelCounts[topLevelId]++;
            }

            Console.WriteLine("\nMost common top levels for braces:");
            foreach (var entry in topLevelCounts.OrderByDescending(kvp => kvp.Value).Take(5))
            {
                string levelName = "Unknown";
                string floorTypeName = "Unknown";
                string floorTypeId = "Unknown";

                if (levelInfo.TryGetValue(entry.Key, out var info))
                {
                    levelName = info.Name;
                    floorTypeId = info.FloorTypeId;

                    if (floorTypeNames.TryGetValue(floorTypeId, out string name))
                    {
                        floorTypeName = name;
                    }
                }

                Console.WriteLine($"  Level: {levelName} (ID: {entry.Key}) - Floor Type: {floorTypeName} (ID: {floorTypeId}) - Brace Count: {entry.Value}");
            }
        }
    }
}
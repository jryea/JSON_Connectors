using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace ModelDebugger
{
    internal class QuickJsonInspector
    {
        internal static void RunQuickInspector(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: QuickJsonInspector.exe <jsonfilepath>");
                return;
            }

            string jsonFilePath = args[0];
            if (!File.Exists(jsonFilePath))
            {
                Console.WriteLine($"File not found: {jsonFilePath}");
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(jsonFilePath);
                using (JsonDocument document = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = document.RootElement;

                    // Quickly explore key relationships
                    Console.WriteLine("=== QUICK JSON ANALYSIS ===");

                    // Analyze floor types
                    JsonElement floorTypesElement = root.GetProperty("modelLayout").GetProperty("floorTypes");
                    int floorTypeCount = floorTypesElement.GetArrayLength();
                    Console.WriteLine($"\nFloor Types: {floorTypeCount} found");

                    // Print first few floor types as a sample
                    for (int i = 0; i < Math.Min(5, floorTypeCount); i++)
                    {
                        var floorType = floorTypesElement[i];
                        Console.WriteLine($"  {i}: {floorType.GetProperty("name").GetString()} (ID: {floorType.GetProperty("id").GetString()})");
                    }

                    // Analyze levels
                    JsonElement levelsElement = root.GetProperty("modelLayout").GetProperty("levels");
                    int levelCount = levelsElement.GetArrayLength();
                    Console.WriteLine($"\nLevels: {levelCount} found");

                    // Organize levels by floor type
                    var levelsByFloorType = new Dictionary<string, List<JsonElement>>();

                    for (int i = 0; i < levelCount; i++)
                    {
                        var level = levelsElement[i];
                        string floorTypeId = "null";

                        if (level.TryGetProperty("floorTypeId", out JsonElement floorTypeIdElement))
                        {
                            floorTypeId = floorTypeIdElement.GetString() ?? "null";
                        }

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
                        Console.WriteLine($"  Floor Type ID: {entry.Key}");
                        foreach (var level in entry.Value)
                        {
                            Console.WriteLine($"    Level: {level.GetProperty("name").GetString()} (ID: {level.GetProperty("id").GetString()}) - Elevation: {level.GetProperty("elevation").GetDouble()}");
                        }
                    }

                    // Check for inconsistencies
                    if (root.TryGetProperty("elements", out JsonElement elementsElement) &&
                        elementsElement.TryGetProperty("beams", out JsonElement beamsElement))
                    {
                        int beamCount = beamsElement.GetArrayLength();

                        // Collect all level IDs
                        var allLevelIds = new HashSet<string>();
                        for (int i = 0; i < levelCount; i++)
                        {
                            allLevelIds.Add(levelsElement[i].GetProperty("id").GetString());
                        }

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
                            for (int i = 0; i < levelCount; i++)
                            {
                                if (levelsElement[i].GetProperty("id").GetString() == entry.Key)
                                {
                                    levelName = levelsElement[i].GetProperty("name").GetString();
                                    break;
                                }
                            }
                            Console.WriteLine($"  Level: {levelName} (ID: {entry.Key}) - Beam Count: {entry.Value}");
                        }
                    }

                    // Check for column relationships
                    if (root.TryGetProperty("elements", out JsonElement elementsElement2) &&
                        elementsElement2.TryGetProperty("columns", out JsonElement columnsElement))
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
                            for (int i = 0; i < levelCount; i++)
                            {
                                if (levelsElement[i].GetProperty("id").GetString() == entry.Key)
                                {
                                    levelName = levelsElement[i].GetProperty("name").GetString();
                                    break;
                                }
                            }
                            Console.WriteLine($"  Level: {levelName} (ID: {entry.Key}) - Column Count: {entry.Value}");
                        }
                    }

                    // Similarly for braces
                    if (root.TryGetProperty("elements", out JsonElement elementsElement3) &&
                        elementsElement3.TryGetProperty("braces", out JsonElement bracesElement))
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
                            for (int i = 0; i < levelCount; i++)
                            {
                                if (levelsElement[i].GetProperty("id").GetString() == entry.Key)
                                {
                                    levelName = levelsElement[i].GetProperty("name").GetString();
                                    break;
                                }
                            }
                            Console.WriteLine($"  Level: {levelName} (ID: {entry.Key}) - Brace Count: {entry.Value}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing JSON: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }

}
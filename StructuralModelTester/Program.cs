using System;
using System.IO;
using Core.Models;
using Core.Converters;
using Core.Utilities;
using RAM;

namespace StructuralModelTester
{
    class Program
    {
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
            while (true)
            {
                Console.WriteLine("\nSelect an option:");
                Console.WriteLine("1. Test Remove Duplicates");
                Console.WriteLine("2. Convert JSON to RAM");
                Console.WriteLine("3. Convert RAM to JSON");
                Console.WriteLine("4. Analyze Model");
                Console.WriteLine("0. Exit");

                Console.Write("\nOption: ");
                string input = Console.ReadLine();

                switch (input)
                {
                    case "0":
                        return;
                    case "1":
                        TestRemoveDuplicates();
                        break;
                    case "2":
                        ConvertJsonToRam();
                        break;
                    case "3":
                        ConvertRamToJson();
                        break;
                    case "4":
                        AnalyzeModel();
                        break;
                    default:
                        Console.WriteLine("Invalid option.");
                        break;
                }
            }
        }

        static void TestRemoveDuplicates()
        {
            Console.Write("Input JSON file path: ");
            string inputPath = Console.ReadLine();
            Console.Write("Output JSON file path: ");
            string outputPath = Console.ReadLine();

            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"File not found: {inputPath}");
                return;
            }

            try
            {
                // Load the model
                BaseModel model = JsonConverter.LoadFromFile(inputPath);

                // Count elements before
                int beamsBefore = model.Elements.Beams?.Count ?? 0;
                int columnsBefore = model.Elements.Columns?.Count ?? 0;
                int wallsBefore = model.Elements.Walls?.Count ?? 0;

                Console.WriteLine($"Before: {beamsBefore} beams, {columnsBefore} columns, {wallsBefore} walls");

                // Remove duplicates
                model.RemoveDuplicateGeometry();

                // Count elements after
                int beamsAfter = model.Elements.Beams?.Count ?? 0;
                int columnsAfter = model.Elements.Columns?.Count ?? 0;
                int wallsAfter = model.Elements.Walls?.Count ?? 0;

                Console.WriteLine($"After: {beamsAfter} beams, {columnsAfter} columns, {wallsAfter} walls");
                Console.WriteLine($"Removed: {beamsBefore - beamsAfter} beams, {columnsBefore - columnsAfter} columns, {wallsBefore - wallsAfter} walls");

                // Save the model
                JsonConverter.SaveToFile(model, outputPath);

                Console.WriteLine($"Saved cleaned model to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void ConvertJsonToRam()
        {
            Console.Write("Input JSON file path: ");
            string jsonPath = Console.ReadLine();
            Console.Write("Output RAM file path: ");
            string ramPath = Console.ReadLine();

            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"File not found: {jsonPath}");
                return;
            }

            try
            {
                var converter = new JSONToRAMConverter();
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
                var converter = new RAMToJSONConverter();
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
            Console.Write("JSON file path: ");
            string jsonPath = Console.ReadLine();

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
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
    }
}
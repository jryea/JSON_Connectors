using System;
using System.IO;
using RAM;

namespace Worker
{
    /// <summary>
    /// Console application that performs conversions in an isolated process
    /// This avoids SQLite conflicts when called from Revit
    /// </summary>
    
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                // Validate command line arguments
                if (args.Length != 2)
                {
                    Console.Error.WriteLine("Usage: RAMWorker.exe <ramFilePath> <outputJsonPath>");
                    Console.Error.WriteLine("Arguments provided: " + string.Join(", ", args));
                    return 1;
                }

                string ramFilePath = args[0];
                string outputJsonPath = args[1];

                // Validate input file exists
                if (!File.Exists(ramFilePath))
                {
                    Console.Error.WriteLine($"RAM file not found: {ramFilePath}");
                    return 2;
                }

                // Log what we're doing
                Console.WriteLine($"Converting RAM file: {ramFilePath}");
                Console.WriteLine($"Output JSON path: {outputJsonPath}");

                // Create output directory if needed
                string outputDir = Path.GetDirectoryName(outputJsonPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    Console.WriteLine($"Created output directory: {outputDir}");
                }

                // Perform the conversion using existing RAMExporter
                var ramExporter = new RAMExporter();
                var conversionResult = ramExporter.ConvertRAMToJSON(ramFilePath);

                if (!conversionResult.Success)
                {
                    Console.Error.WriteLine($"RAM conversion failed: {conversionResult.Message}");
                    return 3;
                }

                // Write JSON output to file
                File.WriteAllText(outputJsonPath, conversionResult.JsonOutput);
                Console.WriteLine($"Successfully wrote JSON to: {outputJsonPath}");

                // Log some basic stats about the output
                var fileInfo = new FileInfo(outputJsonPath);
                Console.WriteLine($"Output file size: {fileInfo.Length:N0} bytes");

                Console.WriteLine("RAM conversion completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception during RAM conversion: {ex.Message}");
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                return 99;
            }
        }
    }
}
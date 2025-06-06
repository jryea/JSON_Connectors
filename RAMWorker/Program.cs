using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using RAM;
using Core.Converters;

namespace Worker
{
    class Program
    {
        [STAThread] // Critical for COM interop with RAM
        static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("Worker.exe started");
                Console.WriteLine($"Arguments: {string.Join(" ", args)}");
                Console.WriteLine($"Working Directory: {Directory.GetCurrentDirectory()}");
                Console.WriteLine($"Assembly Location: {typeof(Program).Assembly.Location}");

                if (args.Length != 2)
                {
                    Console.WriteLine("Usage: Worker.exe <ram_file_path> <output_json_path>");
                    return 1;
                }

                string ramFilePath = args[0];
                string outputJsonPath = args[1];

                Console.WriteLine($"Input RAM file: {ramFilePath}");
                Console.WriteLine($"Output JSON file: {outputJsonPath}");

                // Verify input file exists
                if (!File.Exists(ramFilePath))
                {
                    Console.WriteLine($"ERROR: RAM file not found: {ramFilePath}");
                    return 2;
                }

                // Initialize COM explicitly
                Console.WriteLine("Initializing COM...");
                CoInitializeEx(IntPtr.Zero, COINIT.COINIT_APARTMENTTHREADED);

                try
                {
                    Console.WriteLine("Creating RAMExporter...");
                    var ramExporter = new RAMExporter();

                    Console.WriteLine("Converting RAM to JSON...");
                    var result = ramExporter.ConvertRAMToJSON(ramFilePath);

                    if (result.Success)
                    {
                        Console.WriteLine("Conversion successful, writing output file...");
                        File.WriteAllText(outputJsonPath, result.JsonOutput);
                        Console.WriteLine($"JSON written to: {outputJsonPath}");
                        Console.WriteLine("Worker.exe completed successfully");
                        return 0;
                    }
                    else
                    {
                        Console.WriteLine($"RAM conversion failed: {result.Message}");
                        return 3;
                    }
                }
                finally
                {
                    // Clean up COM
                    Console.WriteLine("Uninitializing COM...");
                    CoUninitialize();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker.exe exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return 4;
            }
        }

        // COM initialization P/Invoke declarations
        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr pvReserved, COINIT dwCoInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        private enum COINIT
        {
            COINIT_APARTMENTTHREADED = 0x2,
            COINIT_MULTITHREADED = 0x0,
        }
    }
}
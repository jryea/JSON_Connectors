using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Revit.Import
{
    /// <summary>
    /// Handles RAM conversion using a separate process to avoid SQLite conflicts
    /// </summary>
    public class ProcessIsolatedRAM
    {
        private readonly string _workerExecutablePath;

        public ProcessIsolatedRAM()
        {
            // Worker exe should be deployed alongside the Revit add-in
            _workerExecutablePath = Path.Combine(
                Path.GetDirectoryName(typeof(ProcessIsolatedRAM).Assembly.Location),
                "RAMWorker.exe"
            );
        }

        /// <summary>
        /// Converts RAM file to JSON using isolated process
        /// </summary>
        /// <param name="ramFilePath">Path to RAM file</param>
        /// <param name="outputJsonPath">Path for output JSON</param>
        /// <returns>Conversion result</returns>
        public RAMConversionResult ConvertRAMToJSON(string ramFilePath, string outputJsonPath)
        {
            try
            {
                // Validate input file exists
                if (!File.Exists(ramFilePath))
                {
                    return new RAMConversionResult
                    {
                        Success = false,
                        Message = $"RAM file not found: {ramFilePath}"
                    };
                }

                // Validate worker executable exists
                if (!File.Exists(_workerExecutablePath))
                {
                    return new RAMConversionResult
                    {
                        Success = false,
                        Message = $"RAMWorker.exe not found at: {_workerExecutablePath}"
                    };
                }

                // Create output directory if it doesn't exist
                string outputDir = Path.GetDirectoryName(outputJsonPath);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Configure process to run RAMWorker
                var processInfo = new ProcessStartInfo
                {
                    FileName = _workerExecutablePath,
                    Arguments = $"\"{ramFilePath}\" \"{outputJsonPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_workerExecutablePath)
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        return new RAMConversionResult
                        {
                            Success = false,
                            Message = "Failed to start RAMWorker process"
                        };
                    }

                    // Read output and error streams
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    // Wait for completion with timeout (5 minutes)
                    bool completed = process.WaitForExit(300000);

                    if (!completed)
                    {
                        process.Kill();
                        return new RAMConversionResult
                        {
                            Success = false,
                            Message = "RAM conversion timed out after 5 minutes"
                        };
                    }

                    // Check exit code
                    if (process.ExitCode == 0)
                    {
                        // Verify output file was created
                        if (File.Exists(outputJsonPath))
                        {
                            return new RAMConversionResult
                            {
                                Success = true,
                                Message = "RAM conversion completed successfully",
                                JsonOutput = File.ReadAllText(outputJsonPath)
                            };
                        }
                        else
                        {
                            return new RAMConversionResult
                            {
                                Success = false,
                                Message = "Conversion completed but output file was not created"
                            };
                        }
                    }
                    else
                    {
                        return new RAMConversionResult
                        {
                            Success = false,
                            Message = $"RAMWorker failed with exit code {process.ExitCode}. Error: {error}. Output: {output}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new RAMConversionResult
                {
                    Success = false,
                    Message = $"Exception during RAM conversion: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Async version of ConvertRAMToJSON
        /// </summary>
        public async Task<RAMConversionResult> ConvertRAMToJSONAsync(string ramFilePath, string outputJsonPath)
        {
            return await Task.Run(() => ConvertRAMToJSON(ramFilePath, outputJsonPath));
        }
    }

    /// <summary>
    /// Result of RAM conversion operation
    /// </summary>
    public class RAMConversionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string JsonOutput { get; set; }
    }
}
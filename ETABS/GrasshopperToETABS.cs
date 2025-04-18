using System;
using System.IO;
using Core.Models;
using Core.Converters;
//using ETABS.Export;
using ETABS.ToETABS;
using ETABS.Utilities;

namespace ETABS
{
    /// <summary>
    /// Provides integration between JSON structural models and ETABS E2K export
    /// </summary>
    public class GrasshopperToETABS
    {
        /// <summary>
        /// Converts JSON model to E2K and handles file operations
        /// </summary>
        /// <param name="jsonString">Input JSON structural model</param>
        /// <param name="customE2K">Optional custom E2K content</param>
        /// <param name="outputPath">Path to save E2K file (if provided)</param>
        /// <returns>Final E2K content string</returns>
        public string ProcessModel(string jsonString, string customE2K = null, string outputPath = null)
        {
            try
            {
                // Step 1: Parse JSON to model
                BaseModel model = JsonConverter.Deserialize(jsonString);

                // Step 2: Convert model to E2K
                var e2kExport = new ModelToETABS();
                string baseE2K = e2kExport.ExportToE2K(model);

                // Step 3: Apply any custom content
                string finalE2kContent = baseE2K;
                if (!string.IsNullOrWhiteSpace(customE2K))
                {
                    var injector = new E2KInjector();
                    finalE2kContent = injector.InjectCustomE2K(baseE2K, customE2K);
                }

                // Step 4: Write to file if path provided
                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    WriteE2KToFile(finalE2kContent, outputPath);
                }

                // Step 5: Return the exact same content
                return finalE2kContent;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in E2K conversion process: {ex.Message}", ex);
            }
        }

        // Writes E2K content to a file
        /// <param name="e2kContent">E2K content to write</param>
        /// <param name="filePath">File path</param>
        private void WriteE2KToFile(string e2kContent, string filePath)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write content to file
                File.WriteAllText(filePath, e2kContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error writing E2K to file: {ex.Message}", ex);
            }
        }
    }
}
﻿using System;
using System.IO;
using Core.Models;
using Core.Converters;
using ETABS.Import;
using ETABS.Utilities;

namespace ETABS
{
    // Provides integration between JSON structural models and ETABS E2K export
    public class ETABSImport
    {
        // Converts JSON model to E2K and handles file operations
       
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
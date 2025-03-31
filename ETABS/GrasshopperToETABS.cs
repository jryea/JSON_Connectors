using System;
using System.IO;
using Core.Models;
using Core.Converters;
using ETABS.Export;
using ETABS.Utilities;
using System.Collections.Generic;

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
                var e2kExport = new ModelToE2K();
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

                // Step 5: Return the final E2K content
                return finalE2kContent;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in E2K conversion process: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Processes a model with additional options for load definitions and surface loads
        /// </summary>
        /// <param name="jsonString">Input JSON structural model</param>
        /// <param name="loadDefs">Custom load definitions JSON string</param>
        /// <param name="surfaceLoads">Custom surface loads JSON string</param>
        /// <param name="customE2K">Optional custom E2K content</param>
        /// <param name="outputPath">Path to save E2K file (if provided)</param>
        /// <returns>Final E2K content string</returns>
        public string ProcessModelWithLoads(string jsonString, string loadDefs = null,
                                           string surfaceLoads = null,
                                           string customE2K = null, string outputPath = null)
        {
            try
            {
                // Step 1: Parse JSON to model
                BaseModel model = JsonConverter.Deserialize(jsonString);

                // Step 2: Add any custom loads if provided
                if (!string.IsNullOrWhiteSpace(loadDefs))
                {
                    // Parse and add load definitions
                    try
                    {
                        var loadDefinitions = JsonConverter.DeserializeLoadDefinitions(loadDefs);
                        if (loadDefinitions != null && loadDefinitions.Count > 0)
                        {
                            model.Loads.LoadDefinitions.AddRange(loadDefinitions);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to parse load definitions: {ex.Message}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(surfaceLoads))
                {
                    // Parse and add surface loads
                    try
                    {
                        var parsedSurfaceLoads = JsonConverter.DeserializeSurfaceLoads(surfaceLoads);
                        if (parsedSurfaceLoads != null && parsedSurfaceLoads.Count > 0)
                        {
                            model.Loads.SurfaceLoads.AddRange(parsedSurfaceLoads);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to parse surface loads: {ex.Message}");
                    }
                }

                // Step 3: Convert model to E2K
                var e2kExport = new ModelToE2K();
                string baseE2K = e2kExport.ExportToE2K(model);

                // Step 4: Apply any custom content
                string finalE2kContent = baseE2K;
                if (!string.IsNullOrWhiteSpace(customE2K))
                {
                    var injector = new E2KInjector();
                    finalE2kContent = injector.InjectCustomE2K(baseE2K, customE2K);
                }

                // Step 5: Write to file if path provided
                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    WriteE2KToFile(finalE2kContent, outputPath);
                }

                // Step 6: Return the final E2K content
                return finalE2kContent;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in E2K conversion process: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Writes E2K content to a file
        /// </summary>
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

    /// <summary>
    /// Extensions to JsonConverter to help with load-related parsing
    /// </summary>
    public static class JsonConverterExtensions
    {
        /// <summary>
        /// Deserializes a JSON string to a list of LoadDefinition objects
        /// </summary>
        /// <param name="jsonString">JSON string containing load definitions</param>
        /// <returns>List of LoadDefinition objects</returns>
        public static List<Core.Models.Loads.LoadDefinition> DeserializeLoadDefinitions(this JsonConverter converter, string jsonString)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<Core.Models.Loads.LoadDefinition>>(
                    jsonString,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deserializes a JSON string to a list of SurfaceLoad objects
        /// </summary>
        /// <param name="jsonString">JSON string containing surface loads</param>
        /// <returns>List of SurfaceLoad objects</returns>
        public static List<Core.Models.Loads.SurfaceLoad> DeserializeSurfaceLoads(this JsonConverter converter, string jsonString)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<Core.Models.Loads.SurfaceLoad>>(
                    jsonString,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch
            {
                return null;
            }
        }
    }
}
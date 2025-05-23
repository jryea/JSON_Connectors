using System;
using System.IO;
using System.Diagnostics;
using Core.Models;
using Core.Converters;

namespace Revit.Import
{
    // Loads model from various file formats, converting to BaseModel
    public class StructuralModelLoader
    {
        private readonly ImportContext _context;

        public StructuralModelLoader(ImportContext context)
        {
            _context = context;
        }

        public BaseModel LoadModel()
        {
            Debug.WriteLine("StructuralModelLoader: Loading model from file");

            try
            {
                // Convert to JSON if needed
                string jsonPath = ConvertToJson();

                // Load from JSON
                var model = JsonConverter.LoadFromFile(jsonPath);
                if (model == null)
                {
                    throw new Exception("Failed to load model from file");
                }

                // Clean up temp file
                if (jsonPath != _context.FilePath && File.Exists(jsonPath))
                {
                    File.Delete(jsonPath);
                }

                Debug.WriteLine("StructuralModelLoader: Model loaded successfully");
                return model;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StructuralModelLoader: Error loading model: {ex.Message}");
                throw;
            }
        }

        private string ConvertToJson()
        {
            string extension = Path.GetExtension(_context.FilePath).ToLowerInvariant();

            switch (extension)
            {
                case ".json":
                    return _context.FilePath;

                case ".e2k":
                    return ConvertETABSToJson();

                case ".rss":
                    return ConvertRAMToJson();

                default:
                    throw new NotSupportedException($"File format {extension} is not supported for import");
            }
        }

        private string ConvertETABSToJson()
        {
            // Create temporary JSON file path
            string tempJsonPath = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(_context.FilePath) + "_temp.json");

            try
            {
                // Read E2K file content
                string e2kContent = File.ReadAllText(_context.FilePath);

                // Convert ETABS to JSON using ETABS project
                var converter = new ETABS.ETABSToGrasshopper();
                string jsonContent = converter.ProcessE2K(e2kContent);

                // Save JSON to temporary file
                File.WriteAllText(tempJsonPath, jsonContent);

                return tempJsonPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert ETABS file: {ex.Message}", ex);
            }
        }

        private string ConvertRAMToJson()
        {
            // Create temporary JSON file path
            string tempJsonPath = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(_context.FilePath) + "_temp.json");

            try
            {
                // Convert RAM to JSON using RAM project
                RAM.RAMExporter ramExporter = new RAM.RAMExporter();
                var conversionResult = ramExporter.ConvertRAMToJSON(_context.FilePath);

                if (!conversionResult.Success)
                {
                    throw new Exception($"RAM conversion failed: {conversionResult.Message}");
                }

                // Save JSON output to temporary file
                File.WriteAllText(tempJsonPath, conversionResult.JsonOutput);

                return tempJsonPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert RAM file: {ex.Message}", ex);
            }
        }
    }
}
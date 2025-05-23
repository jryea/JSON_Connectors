using System;
using System.IO;
using System.Diagnostics;
using Core.Models;
using Core.Converters;

namespace Revit.Import
{
    /// <summary>
    /// Loads model from various file formats, converting to BaseModel
    /// </summary>
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
            // Use existing ETABS import logic
            string tempJsonPath = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(_context.FilePath) + "_temp.json");

            // This would use the existing ETABS import functionality
            // For now, throw not implemented - will be handled by ETABS project
            throw new NotImplementedException("ETABS E2K import conversion not yet implemented. Please convert to JSON format first.");
        }

        private string ConvertRAMToJson()
        {
            // Use existing RAM import logic
            string tempJsonPath = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(_context.FilePath) + "_temp.json");

            // This would use the existing RAM import functionality
            // For now, throw not implemented - will be handled by RAM project
            throw new NotImplementedException("RAM RSS import conversion not yet implemented. Please convert to JSON format first.");
        }
    }
}
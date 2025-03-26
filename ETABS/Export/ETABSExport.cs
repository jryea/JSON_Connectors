using System;
using System.IO;
using Core.Models;
using Core.Converters;
using ETABS.Core.Export;

namespace ETABS.Core.Export
{
    /// <summary>
    /// Provides integration between JSON structural models and ETABS E2K export
    /// </summary>
    public class ETABSExport
    {
        /// <summary>
        /// Exports a JSON string to ETABS E2K format
        /// </summary>
        /// <param name="jsonString">JSON representation of the structural model</param>
        /// <param name="outputPath">Path to save the E2K file</param>
        public void ExportJsonToE2K(string jsonString, string outputPath)
        {
            try
            {
                // Parse JSON to model
                BaseModel model = JsonConverter.Deserialize(jsonString);

                // Export model to E2K
                var exporter = new E2KExporter();
                exporter.ExportToE2K(model, outputPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error exporting JSON to E2K: {ex.Message}", ex);
            }
        }
    }
}
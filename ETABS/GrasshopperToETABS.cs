using System;
using System.IO;
using Core.Models;
using Core.Converters;
using ETABS.Export;

namespace ETABS
{
    /// <summary>
    /// Provides integration between JSON structural models and ETABS E2K export
    /// </summary>
    public class GrasshopperToETABS
    {
        /// <summary>
        /// Exports a JSON string to ETABS E2K format
        /// </summary>
        /// <param name="jsonString">JSON representation of the structural model</param>
        /// <param name="outputPath">Path to save the E2K file</param>
        public void ConvertToE2K(string jsonString, string customE2K, string outputPath)
        {
            try
            {
                // Parse JSON to model
                BaseModel model = JsonConverter.Deserialize(jsonString);

                // Convert model to E2K
                var e2kExport = new ModelToE2K();
                string baseE2K = e2kExport.ExportToE2K(model, outputPath);

                // Inject custom E2K 
                var injector = new E2KInjector();
                string finalE2kContent = injector.InjectCustomE2K(baseE2K, customE2K);

                // Write the complete E2K file
                File.WriteAllText(outputPath, finalE2kContent);

            }
            catch (Exception ex)
            {
                throw new Exception($"Error exporting JSON to E2K: {ex.Message}", ex);
            }
        }
    }
}
// JSONToRAMConverter.cs
using System;
using System.IO;
using Core.Converters;
using Core.Models;
using RAM.Core.Models;
using RAM.Import;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM
{
    /// <summary>
    /// Provides integration between JSON structural models and RAM import
    /// </summary>
    public class JSONToRAMConverter
    {
        /// <summary>
        /// Converts JSON model to RAM model
        /// </summary>
        /// <param name="jsonFilePath">Path to the input JSON file</param>
        /// <param name="ramFilePath">Path to save the RAM file</param>
        /// <returns>True if conversion successful, false otherwise</returns>
        public bool ConvertJSONToRAM(string jsonFilePath, string ramFilePath)
        {
            try
            {
                // Read JSON file
                string jsonContent = File.ReadAllText(jsonFilePath);

                // Parse JSON to base model
                BaseModel model = JsonConverter.Deserialize(jsonContent);

                // Create RAM model file
                using (var modelManager = new RAMModelManager())
                {
                    // Create new model with English units
                    bool result = modelManager.CreateNewModel(ramFilePath, EUnits.eUnitsEnglish);
                    if (!result)
                    {
                        Console.WriteLine("Failed to create RAM model file.");
                        return false;
                    }

                    // Export model to RAM
                    var ramExporter = new Export.RAMExporter();
                    result = ramExporter.ExportModel(model, ramFilePath);

                    if (!result)
                    {
                        Console.WriteLine("Failed to export model to RAM.");
                        return false;
                    }

                    // Save model
                    modelManager.SaveModel();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting JSON to RAM: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts RAM model to JSON model
        /// </summary>
        /// <param name="ramFilePath">Path to the input RAM file</param>
        /// <param name="jsonFilePath">Path to save the JSON file</param>
        /// <returns>True if conversion successful, false otherwise</returns>
        public bool ConvertRAMToJSON(string ramFilePath, string jsonFilePath)
        {
            try
            {
                // Import RAM model to base model
                BaseModel model;

                using (var modelManager = new RAMModelManager())
                {
                    // Open existing RAM model
                    bool result = modelManager.OpenModel(ramFilePath);
                    if (!result)
                    {
                        Console.WriteLine("Failed to open RAM model file.");
                        return false;
                    }

                    // Import model from RAM
                    var ramImporter = new ModelToRAM();
                    model = ramImporter.ImportModel(ramFilePath);
                }

                // Convert base model to JSON
                string jsonContent = JsonConverter.Serialize(model);

                // Write JSON file
                File.WriteAllText(jsonFilePath, jsonContent);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting RAM to JSON: {ex.Message}");
                return false;
            }
        }
    }
}
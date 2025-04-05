using System;
using System.IO;
using System.Text;
using Core.Models;
using Core.Converters;
using RAM.Import.ModelLayout;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM
{
    /// <summary>
    /// Provides integration between JSON structural models and RAM
    /// </summary>
    public class JSONToRAMConverter
    {
        /// <summary>
        /// Converts a JSON file to a RAM model
        /// </summary>
        /// <param name="jsonFilePath">Path to the JSON file</param>
        /// <param name="ramFilePath">Path to save the RAM model file</param>
        /// <returns>Result tuple with success flag and message</returns>
        public (bool Success, string Message) ConvertJSONFileToRAM(string jsonFilePath, string ramFilePath)
        {
            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    return (false, $"JSON file not found: {jsonFilePath}");
                }

                string jsonString = File.ReadAllText(jsonFilePath, Encoding.UTF8);
                return ConvertJSONStringToRAM(jsonString, ramFilePath);
            }
            catch (Exception ex)
            {
                return (false, $"Error reading JSON file: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts a JSON string to a RAM model
        /// </summary>
        /// <param name="jsonString">JSON string representing the building model</param>
        /// <param name="ramFilePath">Path to save the RAM model file</param>
        /// <returns>Result tuple with success flag and message</returns>
        public (bool Success, string Message) ConvertJSONStringToRAM(string jsonString, string ramFilePath)
        {
            try
            {
                // Validate JSON string before attempting to deserialize
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return (false, "JSON string is empty or null.");
                }

                // Trim any leading/trailing whitespace that might cause parsing issues
                jsonString = jsonString.Trim();

                // Check if the string starts with a valid JSON character
                if (!(jsonString.StartsWith("{") || jsonString.StartsWith("[")))
                {
                    return (false, "Invalid JSON format. JSON must start with '{' or '['.");
                }

                // Parse JSON to base model
                BaseModel model = JsonConverter.Deserialize(jsonString);

                if (model == null)
                {
                    return (false, "Failed to deserialize JSON model.");
                }

                if (model.ModelLayout == null || model.ModelLayout.FloorTypes == null || model.ModelLayout.FloorTypes.Count == 0)
                {
                    return (false, "No floor types found in the model.");
                }

                // Create a new RAM model
                using (var modelManager = new RAMModelManager())
                {
                    // Create new model with English units
                    bool result = modelManager.CreateNewModel(ramFilePath, EUnits.eUnitsEnglish);
                    if (!result)
                    {
                        return (false, "Failed to create RAM model file.");
                    }

                    // Import floor types using the dedicated class
                    var floorTypeImporter = new FloorTypeImport(modelManager.Model);
                    int floorTypeCount = floorTypeImporter.Import(model.ModelLayout.FloorTypes);

                    // Save model
                    modelManager.SaveModel();

                    return (true, $"Successfully created RAM model with {floorTypeCount} floor types.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error converting JSON to RAM: {ex.Message}");
            }
        }
    }
}
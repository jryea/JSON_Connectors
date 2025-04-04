using System;
using System.IO;
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
        /// Converts a JSON model to a RAM model, only creating floor types for now
        /// </summary>
        /// <param name="jsonString">JSON string representing the building model</param>
        /// <param name="ramFilePath">Path to save the RAM model file</param>
        /// <returns>True if successful, false otherwise with error message</returns>
        public (bool Success, string Message) ConvertJSONToRAM(string jsonString, string ramFilePath)
        {
            try
            {
                // Parse JSON to base model
                BaseModel model = JsonConverter.Deserialize(jsonString);

                if (model == null)
                    return (false, "Failed to deserialize JSON model.");

                if (model.ModelLayout == null || model.ModelLayout.FloorTypes == null || model.ModelLayout.FloorTypes.Count == 0)
                    return (false, "No floor types found in the model.");

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
// JSONToRAMConverter.cs
using System;
using System.IO;
using Core.Converters;
using Core.Models;
using RAM.ToRAM;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM
{
    // Provides integration between JSON structural models and RAM import
    // Add this method to JSONToRAMConverter.cs
    
    public class JSONToRAMConverter
    {
        // Converts JSON model to RAM model
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
                    var ramExport = new ModelToRAM();
                    result = ramExport.ExportModel(model, ramFilePath);

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

        //// Converts RAM model to JSON model
        //public bool ConvertRAMToJSON(string ramFilePath, string jsonFilePath)
        //{
        //    try
        //    {
        //        // Import RAM model to base model
        //        BaseModel model;

        //        using (var modelManager = new RAMModelManager())
        //        {
        //            // Open existing RAM model
        //            bool result = modelManager.OpenModel(ramFilePath);
        //            if (!result)
        //            {
        //                Console.WriteLine("Failed to open RAM model file.");
        //                return false;
        //            }

        //            // Import model from RAM
        //            var ramImporter = new ModelToRAM();
        //            model = ramImporter.ImportModel(ramFilePath);
        //        }

        //        // Convert base model to JSON
        //        string jsonContent = JsonConverter.Serialize(model);

        //        // Write JSON file
        //        File.WriteAllText(jsonFilePath, jsonContent);

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error converting RAM to JSON: {ex.Message}");
        //        return false;
        //    }
        //}
    }
}
using System;
using System.IO;
using Core.Converters;
using Core.Models;
using ETABS;
using ETABS.Utilities;
using RAM;
using StandaloneConverter.Models;

namespace StandaloneConverter.Services
{
    public class ConversionService
    {
        public void Convert(ConversionOptions options, LoggingService logger)
        {
            if (options.IsRamToEtabs)
            {
                ConvertRamToEtabs(options, logger);
            }
            else
            {
                ConvertEtabsToRam(options, logger);
            }
        }

        private void ConvertRamToEtabs(ConversionOptions options, LoggingService logger)
        {
            string jsonContent;

            // Step 1: Convert RAM to JSON
            logger.Log($"Converting RAM file to JSON: {options.InputFilePath}");
            var ramExporter = new RAMExporter();
            var ramResult = ramExporter.ConvertRAMToJSON(options.InputFilePath);

            if (!ramResult.Success)
            {
                logger.Log($"RAM to JSON conversion failed: {ramResult.Message}");
                throw new Exception($"RAM to JSON conversion failed: {ramResult.Message}");
            }

            jsonContent = ramResult.JsonOutput;
            logger.Log("RAM to JSON conversion successful");

            // Step 2: Clean the model (always done)
            logger.Log("Cleaning model (removing duplicates)...");
            var model = JsonConverter.Deserialize(jsonContent, false);
            model.RemoveDuplicateGeometry();
            jsonContent = JsonConverter.Serialize(model);
            logger.Log("Model cleaning complete");

            // Step 3: Save intermediate JSON (always done)
            logger.Log($"Saving intermediate JSON to: {options.IntermediateJsonPath}");
            File.WriteAllText(options.IntermediateJsonPath, jsonContent);

            // Step 4: Convert JSON to E2K
            logger.Log($"Converting JSON to ETABS E2K format");
            var etabsConverter = new ETABSImport();
            string e2kContent = etabsConverter.ProcessModel(jsonContent, null, null);

            // Step 5: Save E2K file
            logger.Log($"Saving ETABS E2K file to: {options.OutputFilePath}");
            File.WriteAllText(options.OutputFilePath, e2kContent);
        }

        private void ConvertEtabsToRam(ConversionOptions options, LoggingService logger)
        {
            string jsonContent;

            // Step 1: Read E2K file
            logger.Log($"Reading ETABS E2K file: {options.InputFilePath}");
            string e2kContent = File.ReadAllText(options.InputFilePath);

            // Step 2: Convert E2K to JSON
            logger.Log("Converting ETABS E2K to JSON");
            var etabsConverter = new ETABSToGrasshopper();
            jsonContent = etabsConverter.ProcessE2K(e2kContent);

            // Step 3: Clean the model (always done)
            logger.Log("Cleaning model (removing duplicates)...");
            var model = JsonConverter.Deserialize(jsonContent, false);
            model.RemoveDuplicateGeometry();
            jsonContent = JsonConverter.Serialize(model);
            logger.Log("Model cleaning complete");

            // Step 4: Save intermediate JSON (always done)
            logger.Log($"Saving intermediate JSON to: {options.IntermediateJsonPath}");
            File.WriteAllText(options.IntermediateJsonPath, jsonContent);

            // Step 5: Convert JSON to RAM
            logger.Log($"Converting JSON to RAM format");
            var ramImporter = new RAMImporter();
            var ramResult = ramImporter.ConvertJSONStringToRAM(jsonContent, options.OutputFilePath);

            if (!ramResult.Success)
            {
                logger.Log($"JSON to RAM conversion failed: {ramResult.Message}");
                throw new Exception($"JSON to RAM conversion failed: {ramResult.Message}");
            }

            logger.Log("JSON to RAM conversion successful");
        }
    }
}
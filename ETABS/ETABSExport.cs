using System;
using System.IO;
using System.Collections.Generic;   
using Core.Models;
using Core.Converters;
using ETABS.Export;
using ETABS.Utilities;

namespace ETABS
{
    public class ETABSToGrasshopper
    {
        public string ProcessE2K(string e2kContent, string outputPath = null)
        {
            try
            {
                // Parse E2K content into sections
                var e2kParser = new E2KParser();
                Dictionary<string, string> e2kSections = e2kParser.ParseE2K(e2kContent);

                // Convert E2K sections to BaseModel
                var e2kImport = new ETABSToModel();
                BaseModel model = e2kImport.ImportFromE2K(e2kSections);

                // Serialize to JSON
                string jsonContent = JsonConverter.Serialize(model);

                // Write to file if path provided
                if (!string.IsNullOrWhiteSpace(outputPath))
                    File.WriteAllText(outputPath, jsonContent);

                return jsonContent;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in E2K conversion process: {ex.Message}", ex);
            }
        }
    }

}
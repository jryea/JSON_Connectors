using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Models.ModelLayout;
using Core.Models;  

namespace Core.Converters
{
    public static class JsonConverter
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Serialize(BaseModel baseModel)
        {
            try
            {
                return JsonSerializer.Serialize(baseModel, _options);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error serializing building structure: {ex.Message}", ex);
            }
        }

        public static string SerializeWithCleanup(BaseModel baseModel)
        {
            if (baseModel == null)
                return null;

            // Create a deep copy to avoid modifying the original
            BaseModel cleanModel = DeepCopy(baseModel);

            // Remove duplicates on the copy
            cleanModel.RemoveDuplicateGeometry();

            // Serialize the clean copy
            return Serialize(cleanModel);
        }

        private static BaseModel DeepCopy(BaseModel original)
        {
            string json = Serialize(original);
            return Deserialize(json);
        }

        public static BaseModel Deserialize(string json, bool removeDuplicates = true)
        {
            try
            {
                BaseModel model = JsonSerializer.Deserialize<BaseModel>(json, _options);

                // Remove duplicate elements
                if (removeDuplicates && model != null)
                {
                    model.RemoveDuplicateGeometry();
                }

                return model;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deserializing building structure: {ex.Message}", ex);
            }
        }

        public static void SaveToFile(BaseModel baseModel, string filePath, bool removeDuplicates = true)
        {
            try
            {
                // remove duplicate elements before saving  
                if (removeDuplicates && baseModel != null)
                {
                    baseModel.RemoveDuplicateGeometry();
                }

                string json = Serialize(baseModel);
                File.WriteAllText(filePath, json);
            }

            catch (UnauthorizedAccessException ex)
            {
                throw new Exception($"Access to the path '{filePath}' is denied: {ex.Message}", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new Exception($"The directory specified in path '{filePath}' was not found: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new Exception($"An I/O error occurred while writing to the file '{filePath}': {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving building structure to file: {ex.Message}", ex);
            }
        }

        public static BaseModel LoadFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                return Deserialize(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading building structure from file: {ex.Message}", ex);
            }
        }
    }
}
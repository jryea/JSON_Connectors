using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Models.Model;
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

        public static BaseModel Deserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<BaseModel>(json, _options);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deserializing building structure: {ex.Message}", ex);
            }
        }

        public static void SaveToFile(BaseModel baseModel, string filePath)
        {
            try
            {
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
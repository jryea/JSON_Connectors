using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Models.Model;

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

        public static string Serialize(Model buildingStructure)
        {
            try
            {
                return JsonSerializer.Serialize(buildingStructure, _options);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error serializing building structure: {ex.Message}", ex);
            }
        }

        public static Model Deserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Model>(json, _options);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deserializing building structure: {ex.Message}", ex);
            }
        }

        public static void SaveToFile(Model buildingStructure, string filePath)
        {
            try
            {
                string json = Serialize(buildingStructure);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving building structure to file: {ex.Message}", ex);
            }
        }

        public static Model LoadFromFile(string filePath)
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
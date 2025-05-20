using System;
using System.IO;
using System.Text;
using Core.Models;
using CC = Core.Converters;
using RAM.Import.ModelLayout;
using RAM.Import.Elements;
using RAM.Import.Loads;
using RAM.Utilities;
using RAMDATAACCESSLib;
using RAM.Import.Properties;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Core.Models.ModelLayout;
using System.Collections.Generic;

namespace RAM
{
    // Provides integration between JSON structural models and RAM
    public class RAMImporter
    {
        private readonly MaterialProvider _materialProvider;

        public RAMImporter()
        {
            _materialProvider = new MaterialProvider();
        }

        // Converts a JSON file to a RAM model
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

        // Converts a JSON string to a RAM model
        public (bool Success, string Message) ConvertJSONStringToRAM(string jsonString, string ramFilePath)
        {
            try
            {
                Console.WriteLine("Starting JSON to RAM conversion");

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

                Console.WriteLine("JSON validation passed, attempting deserialization");

                BaseModel model = null;

                try
                {
                    // Use existing deserializer
                    model = CC.JsonConverter.Deserialize(jsonString);
                    Console.WriteLine("Deserialization successful");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Deserialization failed: {ex.Message}");
                    return (false, $"Failed to deserialize JSON model: {ex.Message}");
                }

                if (model == null)
                {
                    return (false, "Failed to deserialize JSON model.");
                }

                if (model.ModelLayout == null)
                {
                    return (false, "No model layout found in the model.");
                }

                Console.WriteLine("Model deserialized successfully");

                // Create a new RAM model
                using (var modelManager = new RAMModelManager())
                {
                    Console.WriteLine("Creating RAM model manager");

                    // Determine the appropriate units from the model
                    EUnits ramUnits = EUnits.eUnitsEnglish;  // Default to English units
                    string lengthUnit = "inches";

                    if (model.Metadata != null && model.Metadata.Units != null)
                    {
                        lengthUnit = model.Metadata.Units.Length ?? "inches";

                        // Determine RAM units based on model units
                        if (lengthUnit.ToLower().Contains("meter") ||
                            lengthUnit.ToLower().Contains("millimeter") ||
                            lengthUnit.ToLower().Contains("centimeter"))
                        {
                            ramUnits = EUnits.eUnitsMetric;
                        }
                    }

                    Console.WriteLine($"Using length unit: {lengthUnit}, RAM units: {ramUnits}");

                    // Create new model with appropriate units
                    Console.WriteLine($"Creating new RAM model at: {ramFilePath}");
                    bool result = modelManager.CreateNewModel(ramFilePath, ramUnits);
                    if (!result)
                    {
                        return (false, "Failed to create RAM model file.");
                    }

                    Console.WriteLine("RAM model created successfully");

                    try
                    {
                        // Filter valid levels first
                        Console.WriteLine("Filtering valid levels");
                        var validLevels = ModelLayoutFilter.GetValidLevels(model.ModelLayout.Levels);
                        Console.WriteLine($"Found {validLevels.Count()} valid levels");

                        // Create the level-to-floor-type mapping using valid levels
                        Console.WriteLine("Creating level-to-floor-type mapping");
                        var levelToFloorTypeMapping = ModelMappingUtility.CreateLevelToFloorTypeMapping(validLevels);
                        Console.WriteLine($"Created {levelToFloorTypeMapping.Count} level-to-floor-type mappings");

                        // Import floor types
                        Console.WriteLine("Importing floor types");
                        var floorTypeImporter = new FloorTypeImport(modelManager.Model);
                        int floorTypeCount = 0;
                        if (model.ModelLayout.FloorTypes != null && model.ModelLayout.FloorTypes.Count > 0)
                        {
                            // Sort floor types by elevation before importing
                            var sortedFloorTypes = SortFloorTypesByElevation(model.ModelLayout.FloorTypes, validLevels);

                            // Log the sorted order
                            Console.WriteLine("Floor types sorted by elevation for importing:");
                            foreach (var ft in sortedFloorTypes)
                            {
                                Console.WriteLine($"  {ft.Name} (ID: {ft.Id})");
                            }

                            floorTypeCount = floorTypeImporter.Import(sortedFloorTypes, validLevels);
                            Console.WriteLine($"Imported {floorTypeCount} floor types");
                        }

                        // Import stories
                        Console.WriteLine("Importing stories");
                        var storyImporter = new StoryImport(modelManager.Model, lengthUnit);
                        int storyCount = 0;
                        if (model.ModelLayout.Levels != null && model.ModelLayout.Levels.Count > 0)
                        {
                            storyImporter.SetFloorTypeMapping(model.ModelLayout.FloorTypes);
                            storyCount = storyImporter.Import(validLevels);
                            Console.WriteLine($"Imported {storyCount} stories");
                        }

                        // Import grid lines
                        string gridSystemName = "StandardGrids";
                        if (model.Metadata != null && model.Metadata.ProjectInfo != null &&
                            !string.IsNullOrEmpty(model.Metadata.ProjectInfo.ProjectName))
                        {
                            gridSystemName = $"{model.Metadata.ProjectInfo.ProjectName}Grids";
                        }

                        Console.WriteLine($"Importing grids with system name: {gridSystemName}");
                        var gridImporter = new GridImport(modelManager.Model, lengthUnit, gridSystemName);
                        int gridCount = 0;
                        if (model.ModelLayout.Grids != null && model.ModelLayout.Grids.Count > 0)
                        {
                            gridCount = gridImporter.Import(model.ModelLayout.Grids);
                            Console.WriteLine($"Imported {gridCount} grids");
                        }

                        // Import surface load properties
                        Console.WriteLine("Importing surface load properties");
                        int surfaceLoadCount = 0;
                        if (model.Loads != null && model.Loads.SurfaceLoads != null && model.Loads.SurfaceLoads.Count > 0)
                        {
                            var surfaceLoadImporter = new SurfaceLoadPropertiesImport(modelManager.Model);
                            surfaceLoadCount = surfaceLoadImporter.Import(
                                model.Loads.SurfaceLoads,
                                model.Loads.LoadDefinitions);
                            Console.WriteLine($"Imported {surfaceLoadCount} surface loads");
                        }

                        // Import properties
                        Console.WriteLine("Importing floor properties");
                        if (model.Properties.FloorProperties != null && model.Properties.FloorProperties.Count > 0)
                        {
                            var floorPropertiesImporter = new FloorPropertiesImport(
                                modelManager.Model,
                                _materialProvider,
                                lengthUnit);
                            var floorPropertyMappings = floorPropertiesImporter.Import(model.Properties.FloorProperties);
                            Console.WriteLine($"Imported {floorPropertyMappings.Count} floor property mappings");
                        }

                        // Modified section of RAMImporter.cs
                        // This replaces the relevant section in the Import method where beam, column and brace importing happens

                        // Import structural elements
                        int beamCount = 0;
                        int columnCount = 0;
                        int braceCount = 0;
                        int wallCount = 0;
                        int isolatedFootingCount = 0;

                        if (model.Elements != null)
                        {
                            // Create a detailed level-to-floor-type mapping
                            Console.WriteLine("Creating detailed level-to-floor-type mapping...");
                            var detailedLevelToFloorTypeMap = ModelMappingUtility.CreateLevelToFloorTypeMapping(validLevels);

                            // Dump the mapping for debugging
                            foreach (var mapping in detailedLevelToFloorTypeMap)
                            {
                                var level = validLevels.FirstOrDefault(l => l.Id == mapping.Key);
                                Console.WriteLine($"Level: {(level != null ? level.Name : "Unknown")} (ID: {mapping.Key}) -> FloorType ID: {mapping.Value}");
                            }

                            // Import beams
                            Console.WriteLine("Importing beams");
                            if (model.Elements.Beams != null && model.Elements.Beams.Count > 0)
                            {
                                var beamImporter = new BeamImport(
                                    modelManager.Model,
                                    _materialProvider,
                                    lengthUnit);

                                beamCount = beamImporter.Import(
                                    model.Elements.Beams,
                                    validLevels,
                                    model.Properties.FrameProperties,
                                    detailedLevelToFloorTypeMap);
                                Console.WriteLine($"Imported {beamCount} beams");
                            }

                            // Import columns
                            Console.WriteLine("Importing columns");
                            if (model.Elements.Columns != null && model.Elements.Columns.Count > 0)
                            {
                                var columnImporter = new ColumnImport(
                                    modelManager.Model,
                                    _materialProvider,
                                    lengthUnit);

                                columnCount = columnImporter.Import(
                                    model.Elements.Columns,
                                    validLevels,
                                    model.Properties.FrameProperties,
                                    detailedLevelToFloorTypeMap);
                                Console.WriteLine($"Imported {columnCount} columns");
                            }

                            // Import braces
                            Console.WriteLine("Importing braces");
                            if (model.Elements.Braces != null && model.Elements.Braces.Count > 0)
                            {
                                var braceImporter = new BraceImport(
                                    modelManager.Model,
                                    _materialProvider,
                                    lengthUnit);

                                braceCount = braceImporter.Import(
                                    model.Elements.Braces,
                                    validLevels,
                                    model.Properties.FrameProperties,
                                    detailedLevelToFloorTypeMap);
                                Console.WriteLine($"Imported {braceCount} braces");
                            }

                            // Import walls
                            Console.WriteLine("Importing walls");
                            if (model.Elements.Walls != null && model.Elements.Walls.Count > 0)
                            {
                                var wallImporter = new WallImport(
                                    modelManager.Model,
                                    _materialProvider,
                                    lengthUnit);

                                wallCount = wallImporter.Import(
                                    model.Elements.Walls,
                                    validLevels,
                                    detailedLevelToFloorTypeMap,
                                    model.Properties.WallProperties);
                                Console.WriteLine($"Imported {wallCount} walls");
                            }

                            // Import isolated footings
                            Console.WriteLine("Importing isolated footings");
                            if (model.Elements.IsolatedFootings != null && model.Elements.IsolatedFootings.Count > 0)
                            {
                                var isolatedFootingImporter = new IsolatedFootingImport(
                                    modelManager.Model,
                                    _materialProvider,
                                    lengthUnit);
                                try
                                {
                                    isolatedFootingCount = isolatedFootingImporter.Import(
                                        model.Elements.IsolatedFootings,
                                        validLevels);
                                    Console.WriteLine($"Imported {isolatedFootingCount} isolated footings");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error importing isolated footings: {ex.Message}");
                                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                                    // Continue without returning, we'll still try to save the model
                                }
                            }
                        }


                        // Save model
                        Console.WriteLine("Saving RAM model");
                        modelManager.SaveModel();
                        Console.WriteLine("RAM model saved successfully");

                        return (true, $"Successfully created RAM model with {floorTypeCount} floor types, {gridCount} grids, {storyCount} stories, {surfaceLoadCount} surface loads, {beamCount} beams, {columnCount} columns, and {wallCount} walls.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during import process: {ex.Message}");
                        Console.WriteLine($"Exception type: {ex.GetType().FullName}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");

                        // Try to save the model anyway
                        try
                        {
                            modelManager.SaveModel();
                            Console.WriteLine("Attempted to save partial model despite error");
                        }
                        catch (Exception saveEx)
                        {
                            Console.WriteLine($"Failed to save partial model: {saveEx.Message}");
                        }

                        return (false, $"Error during import: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception in ConvertJSONStringToRAM: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().FullName}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return (false, $"Error converting JSON to RAM: {ex.Message}");
            }
        }

        private List<FloorType> SortFloorTypesByElevation(IEnumerable<FloorType> floorTypes, IEnumerable<Level> levels)
        {
            // Calculate average elevation for each floor type
            var elevationMap = new Dictionary<string, double>();
            var countMap = new Dictionary<string, int>();

            foreach (var level in levels)
            {
                if (!string.IsNullOrEmpty(level.FloorTypeId))
                {
                    if (!elevationMap.ContainsKey(level.FloorTypeId))
                    {
                        elevationMap[level.FloorTypeId] = 0;
                        countMap[level.FloorTypeId] = 0;
                    }

                    elevationMap[level.FloorTypeId] += level.Elevation;
                    countMap[level.FloorTypeId]++;
                }
            }

            // Calculate averages
            foreach (var key in elevationMap.Keys.ToList())
            {
                if (countMap[key] > 0)
                    elevationMap[key] /= countMap[key];
            }

            // Sort by elevation
            return floorTypes
                .OrderBy(ft => elevationMap.ContainsKey(ft.Id) ? elevationMap[ft.Id] : double.MaxValue)
                .ToList();
        }
    }
}
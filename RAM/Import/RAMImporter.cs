using System;
using System.IO;
using System.Text;
using Core.Models;
using Core.Converters;
using RAM.Import.ModelLayout;
using RAM.Import.Elements;
using RAM.Import.Loads;
using RAM.Utilities;
using RAMDATAACCESSLib;
using RAM.Import.Properties;

namespace RAM
{
    // Provides integration between JSON structural models and RAM
    public class RAMImporter
    {
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

                if (model.ModelLayout == null)
                {
                    return (false, "No model layout found in the model.");
                }

                // Create a new RAM model
                using (var modelManager = new RAMModelManager())
                {
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

                    // Create new model with appropriate units
                    bool result = modelManager.CreateNewModel(ramFilePath, ramUnits);
                    if (!result)
                    {
                        return (false, "Failed to create RAM model file.");
                    }

                    // Filter valid levels first
                    var validLevels = ModelLayoutFilter.GetValidLevels(model.ModelLayout.Levels);

                    // Create the level-to-floor-type mapping using valid levels
                    var levelToFloorTypeMapping = ImportHelpers.CreateLevelToFloorTypeMapping(validLevels);

                    // Import floor types
                    var floorTypeImporter = new FloorTypeImport(modelManager.Model);
                    int floorTypeCount = 0;
                    if (model.ModelLayout.FloorTypes != null && model.ModelLayout.FloorTypes.Count > 0)
                    {
                        floorTypeCount = floorTypeImporter.Import(model.ModelLayout.FloorTypes, validLevels);
                    }

                    // Import stories
                    var storyImporter = new StoryImport(modelManager.Model, lengthUnit);
                    int storyCount = 0;
                    if (model.ModelLayout.Levels != null && model.ModelLayout.Levels.Count > 0)
                    {
                        storyImporter.SetFloorTypeMapping(model.ModelLayout.FloorTypes);
                        storyCount = storyImporter.Import(validLevels);
                    }

                    // Import grid lines
                    string gridSystemName = "StandardGrids";
                    if (model.Metadata != null && model.Metadata.ProjectInfo != null &&
                        !string.IsNullOrEmpty(model.Metadata.ProjectInfo.ProjectName))
                    {
                        gridSystemName = $"{model.Metadata.ProjectInfo.ProjectName}Grids";
                    }

                    var gridImporter = new GridImport(modelManager.Model, lengthUnit, gridSystemName);
                    int gridCount = 0;
                    if (model.ModelLayout.Grids != null && model.ModelLayout.Grids.Count > 0)
                    {
                        gridCount = gridImporter.Import(model.ModelLayout.Grids);
                    }

                    // Import surface load properties
                    int surfaceLoadCount = 0;
                    if (model.Loads != null && model.Loads.SurfaceLoads != null && model.Loads.SurfaceLoads.Count > 0)
                    {
                        var surfaceLoadImporter = new SurfaceLoadPropertiesImport(modelManager.Model);
                        surfaceLoadCount = surfaceLoadImporter.Import(
                            model.Loads.SurfaceLoads,
                            model.Loads.LoadDefinitions);
                    }

                    // Import properties
                    if (model.Properties.FloorProperties != null && model.Properties.FloorProperties.Count > 0)
                    {
                        var slabImporter = new SlabPropertiesImport(modelManager.Model, lengthUnit);
                        var slabMappings = slabImporter.Import(
                            model.Properties.FloorProperties,
                            levelToFloorTypeMapping);
                    }

                    // Import structural elements
                    int beamCount = 0;
                    int columnCount = 0;
                    int braceCount = 0;
                    int wallCount = 0;

                    if (model.Elements != null)
                    {
                        // Import beams
                        if (model.Elements.Beams != null && model.Elements.Beams.Count > 0)
                        {
                            var beamImporter = new BeamImport(modelManager.Model, lengthUnit);
                            beamCount = beamImporter.Import(
                                model.Elements.Beams,
                                validLevels,
                                model.Properties.FrameProperties,
                                model.Properties.Materials,
                                levelToFloorTypeMapping);
                            Console.WriteLine($"Imported {beamCount} beams.");
                        }

                        // Import columns
                        if (model.Elements.Columns != null && model.Elements.Columns.Count > 0)
                        {
                            var columnImporter = new ColumnImport(modelManager.Model, lengthUnit);
                            columnCount = columnImporter.Import(
                                model.Elements.Columns,
                                validLevels,
                                model.Properties.FrameProperties,
                                model.Properties.Materials,
                                levelToFloorTypeMapping);
                        }

                        if (model.Elements.Braces != null && model.Elements.Braces.Count > 0)
                        {
                            var braceImporter = new BraceImport(modelManager.Model, lengthUnit);
                            braceCount = braceImporter.Import(
                                model.Elements.Braces,
                                validLevels,
                                model.Properties.FrameProperties,
                                model.Properties.Materials);
                        }   

                        // Import walls
                        if (model.Elements.Walls != null && model.Elements.Walls.Count > 0)
                        {
                            var wallImporter = new WallImport(modelManager.Model, lengthUnit);
                            wallCount = wallImporter.Import(
                                model.Elements.Walls,
                                validLevels,
                                levelToFloorTypeMapping,
                                model.Properties.WallProperties);
                        }
                    }

                    // Save model
                    modelManager.SaveModel();

                    return (true, $"Successfully created RAM model with {floorTypeCount} floor types, {gridCount} grids, {storyCount} stories, {surfaceLoadCount} surface loads, {beamCount} beams, {columnCount} columns, and {wallCount} walls.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error converting JSON to RAM: {ex.Message}");

            }
        }
    }
}
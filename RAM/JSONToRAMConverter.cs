using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Core.Models;
using Core.Converters;
using RAM.Import.ModelLayout;
using RAM.Import.Elements;
using RAM.Import.Properties;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM
{
    // Provides integration between JSON structural models and RAM
    public class JSONToRAMConverter
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

                    // Import floor types first (required for grids and stories)
                    var floorTypeImporter = new FloorTypeImport(modelManager.Model);
                    int floorTypeCount = 0;
                    if (model.ModelLayout.FloorTypes != null && model.ModelLayout.FloorTypes.Count > 0)
                    {
                        floorTypeCount = floorTypeImporter.Import(model.ModelLayout.FloorTypes);
                    }
                    else
                    {
                        // Create at least one default floor type if none exist
                        IFloorTypes ramFloorTypes = modelManager.Model.GetFloorTypes();
                        ramFloorTypes.Add("Default");
                        floorTypeCount = 1;
                    }

                    // Import floor properties with the new importers
                    Dictionary<string, int> slabPropIds = new Dictionary<string, int>();
                    Dictionary<string, int> compDeckPropIds = new Dictionary<string, int>();
                    Dictionary<string, int> nonCompDeckPropIds = new Dictionary<string, int>();

                    if (model.Properties != null && model.Properties.FloorProperties != null &&
                        model.Properties.FloorProperties.Count > 0)
                    {
                        // Import slab properties
                        var slabPropsImporter = new SlabPropertiesImport(modelManager.Model, lengthUnit);
                        slabPropIds = slabPropsImporter.Import(model.Properties.FloorProperties);

                        // Import composite deck properties
                        var compDeckPropsImporter = new CompositeDeckPropertiesImport(modelManager.Model, lengthUnit);
                        compDeckPropIds = compDeckPropsImporter.Import(model.Properties.FloorProperties);

                        // Import non-composite deck properties
                        var nonCompDeckPropsImporter = new NonCompositeDeckPropertiesImport(modelManager.Model, lengthUnit);
                        nonCompDeckPropIds = nonCompDeckPropsImporter.Import(model.Properties.FloorProperties);
                    }

                    // Import stories/levels (before grids since grids will be assigned to floor types)
                    var storyImporter = new StoryImport(modelManager.Model, lengthUnit);
                    int storyCount = 0;
                    if (model.ModelLayout.Levels != null && model.ModelLayout.Levels.Count > 0)
                    {
                        // Set up floor type mapping first
                        if (model.ModelLayout.FloorTypes != null)
                        {
                            storyImporter.SetFloorTypeMapping(model.ModelLayout.FloorTypes);
                        }

                        storyCount = storyImporter.Import(model.ModelLayout.Levels);
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

                    // Import structural elements
                    int beamCount = 0;
                    int columnCount = 0;
                    int wallCount = 0;
                    int floorCount = 0;

                    if (model.Elements != null)
                    {
                        // Import beams
                        if (model.Elements.Beams != null && model.Elements.Beams.Count > 0)
                        {
                            var beamImporter = new BeamImport(modelManager.Model, lengthUnit);
                            beamCount = beamImporter.Import(
                                model.Elements.Beams,
                                model.ModelLayout.Levels,
                                model.Properties.FrameProperties);
                        }

                        if (model.Elements.Columns != null && model.Elements.Columns.Count > 0)
                        {
                            var columnImporter = new ColumnImport(modelManager.Model, lengthUnit);
                            columnCount = columnImporter.Import(
                                model.Elements.Columns,
                                model.ModelLayout.Levels,
                                model.Properties.FrameProperties);
                        }
                       
                        if (model.Elements.Walls != null && model.Elements.Walls.Count > 0)
                        {
                            var wallImporter = new WallImport(modelManager.Model, lengthUnit);
                            wallCount = wallImporter.Import(
                                model.Elements.Walls,
                                model.ModelLayout.Levels,
                                model.Properties.WallProperties);
                        }
                    }

                    // Save model
                    modelManager.SaveModel();

                    // Calculate property counts
                    int slabPropCount = slabPropIds.Count;
                    int compDeckPropCount = compDeckPropIds.Count;
                    int nonCompDeckPropCount = nonCompDeckPropIds.Count;
                    int totalPropCount = slabPropCount + compDeckPropCount + nonCompDeckPropCount;

                    return (true, $"Successfully created RAM model with {floorTypeCount} floor types, " +
                        $"{gridCount} grids, {storyCount} stories, {totalPropCount} floor properties " +
                        $"({slabPropCount} slabs, {compDeckPropCount} composite decks, {nonCompDeckPropCount} non-composite decks), " +
                        $"{beamCount} beams, {columnCount} columns, {wallCount} walls, and {floorCount} floors.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error converting JSON to RAM: {ex.Message}");
            }
        }
    }
}
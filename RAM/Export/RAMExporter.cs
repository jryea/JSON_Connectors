using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.ModelLayout;
using Core.Models.Elements;
using Core.Models.Properties;
using Core.Models.Geometry;
using Core.Models.Metadata;
using Core.Models.Loads;
using Core.Converters;
using Core.Utilities;
using RAM.Utilities;
using RAM.Export.ModelLayout;
using RAM.Export.Elements;
using RAMDATAACCESSLib;
using RAM.Export.Properties;
using System.Diagnostics;

namespace RAM
{
    public class RAMExporter
    {
        // Main method to convert RAM model to JSON
        public (string JsonOutput, string Message, bool Success) ConvertRAMToJSON(string ramFilePath)
        {
            try
            {
                // Open the RAM model
                using (var modelManager = new RAMModelManager())
                {
                    bool isOpen = modelManager.OpenModel(ramFilePath);
                    if (!isOpen)
                    {
                        return (null, "Failed to open RAM model file.", false);
                    }

                    // Create the base model structure
                    BaseModel model = new BaseModel
                    {
                        Metadata = new MetadataContainer
                        {
                            ProjectInfo = ExtractProjectInfo(modelManager.Model),
                            Units = new Units { Length = "inches", Force = "pounds", Temperature = "fahrenheit" }
                        },
                        ModelLayout = new ModelLayoutContainer(),
                        Properties = new PropertiesContainer(),
                        Elements = new ElementContainer(),
                        Loads = new LoadContainer()
                    };

                    // Setup exporters
                    string lengthUnit = model.Metadata.Units.Length;

                    string version = ($"Version: {modelManager.Model.dVersion}");

                    try
                    {
                        // Extract floor types
                        FloorTypeExport floorTypeExporter = new FloorTypeExport(modelManager.Model);
                        model.ModelLayout.FloorTypes = floorTypeExporter.Export();

                        // Find the Ground floor type ID
                        string groundFloorTypeId = model.ModelLayout.FloorTypes
                            .FirstOrDefault(ft => ft.Name.Equals("Ground", StringComparison.OrdinalIgnoreCase))?.Id;

                        // Create the mapping from RAM UIDs to generated IDs
                        Dictionary<int, string> floorTypeMapping = floorTypeExporter.CreateFloorTypeMapping(model.ModelLayout.FloorTypes);

                        // Set the mapping before extracting levels
                        LevelExport levelExporter = new LevelExport(modelManager.Model, lengthUnit);
                        levelExporter.SetFloorTypeMapping(floorTypeMapping, groundFloorTypeId);
                        model.ModelLayout.Levels = levelExporter.Export();

                        // Extract grids
                        GridExport gridExporter = new GridExport(modelManager.Model, lengthUnit);
                        model.ModelLayout.Grids = gridExporter.Export();

                        // Extract materials
                        model.Properties.Materials = ExtractMaterials();

                        // Initialize mappings using the ModelMappingUtility
                        ModelMappingUtility.InitializeMappings(modelManager.Model, model);

                        model.Properties.Materials = ExtractMaterials();

                        // Extract frame properties
                        var framePropertiesExporter = new FramePropertiesExport(modelManager.Model, lengthUnit);
                        var (frameProps, framePropMappings) = framePropertiesExporter.Export(model.Properties.Materials);
                        model.Properties.FrameProperties = frameProps;

                        // Make sure the frame property mappings are registered
                        ModelMappingUtility.SetFramePropertyMappings(framePropMappings);

                        // Extract floors
                        FloorExport floorExporter = new FloorExport(modelManager.Model, lengthUnit);
                        model.Elements.Floors = floorExporter.Export();
                        Console.WriteLine($"Exported {model.Elements.Floors.Count} floors");

                        // Extract wall properties before walls
                        var wallPropertiesExporter = new WallPropertiesExport(modelManager.Model, lengthUnit);
                        model.Properties.WallProperties = wallPropertiesExporter.Export(model.Properties.Materials);

                        // Now extract structural elements
                        BeamExport beamExporter = new BeamExport(modelManager.Model, lengthUnit);
                        model.Elements.Beams = beamExporter.Export();

                        // Extract walls after wall properties
                        WallExport wallExporter = new WallExport(modelManager.Model, lengthUnit);
                        model.Elements.Walls = wallExporter.Export();


                        // Extract columns using the mapping utility
                        ColumnExport columnExporter = new ColumnExport(modelManager.Model, lengthUnit);
                        model.Elements.Columns = columnExporter.Export();

                        // Extract isolated footings using the mapping utility
                        IsolatedFootingExport isolatedFootingExporter = new IsolatedFootingExport(modelManager.Model, lengthUnit);
                        model.Elements.IsolatedFootings = isolatedFootingExporter.Export();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during extraction: {ex.Message}");
                        // Continue with partial model
                    }

                    // Serialize the model to JSON
                    string jsonString = JsonConverter.Serialize(model);

                    return (jsonString, "Successfully converted RAM model to JSON.", true);
                }
            }
            catch (Exception ex)
            {
                return (null, $"Error converting RAM to JSON: {ex.Message}", false);
            }
        }

        #region Extraction Methods

        // Extract project information
        private ProjectInfo ExtractProjectInfo(IModel ramModel)
        {
            var projectInfo = new ProjectInfo
            {
                ProjectName = "Imported from RAM",
                ProjectId = Guid.NewGuid().ToString(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };

            return projectInfo;
        }

        // Extract materials
        private List<Material> ExtractMaterials()
        {
            var materials = new List<Material>();

            // Add default steel material
            materials.Add(new Material
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                Name = "Steel",
                Type = "Steel",
                DesignData =
                {
                    ["fy"] = 50000.0, // Default Fy = 50 ksi
                    ["fu"] = 65000.0, // Default Fu = 65 ksi
                    ["elasticModulus"] = 29000000.0, // E = 29000 ksi
                    ["weightDensity"] = 490.0, // 490 pcf
                    ["poissonsRatio"] = 0.3
                }
            });

            // Add default concrete material
            materials.Add(new Material
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                Name = "Concrete",
                Type = "Concrete",
                DesignData =
                {
                    ["fc"] = 4000.0, // Default f'c = 4 ksi
                    ["elasticModulus"] = 3600000.0, // Default E = 3600 ksi
                    ["weightDensity"] = 150.0, // 150 pcf
                    ["poissonsRatio"] = 0.2
                }
            });

            return materials;
        }

        #endregion
    }
}
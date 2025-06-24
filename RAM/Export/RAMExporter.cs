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
using RAM.Export.Properties;
using RAMDATAACCESSLib;
using System.Diagnostics;

namespace RAM
{
    public class RAMExporter
    {
        private readonly MaterialProvider _materialProvider;

        public RAMExporter()
        {
            _materialProvider = new MaterialProvider();
        }

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


                        // Extract frame properties with material provider
                        var framePropertiesExporter = new FramePropertiesExport(
                            modelManager.Model,
                            _materialProvider,
                            lengthUnit);
                        model.Properties.FrameProperties = framePropertiesExporter.Export();

                        // Extract floor properties with material provider
                        var floorPropertiesExporter = new FloorPropertiesExport(
                            modelManager.Model,
                            _materialProvider,
                            lengthUnit);
                        model.Properties.FloorProperties = floorPropertiesExporter.Export();
                        Console.WriteLine($"Exported {model.Properties.FloorProperties.Count} floor properties");

                        // Initialize mappings using the ModelMappingUtility
                        ModelMappingUtility.InitializeMappings(modelManager.Model, model);

                        // Extract diaphragm properties
                        model.Properties.Diaphragms = ExtractDiaphragms();

                        // Extract wall properties with material provider
                        var wallPropertiesExporter = new WallPropertiesExport(
                            modelManager.Model,
                            _materialProvider,
                            lengthUnit);
                        model.Properties.WallProperties = wallPropertiesExporter.Export();

                        // Update model with materials from the provider
                        model.Properties.Materials = _materialProvider.GetAllMaterials();

                        // Extract structural elements
                        BeamExport beamExporter = new BeamExport(modelManager.Model, lengthUnit);
                        model.Elements.Beams = beamExporter.Export();

                        // Extract walls
                        WallExport wallExporter = new WallExport(modelManager.Model, lengthUnit);
                        model.Elements.Walls = wallExporter.Export();

                        // Extract floors
                        FloorExport floorExporter = new FloorExport(modelManager.Model, lengthUnit);
                        model.Elements.Floors = floorExporter.Export();

                        // Extract columns using the mapping utility
                        ColumnExport columnExporter = new ColumnExport(modelManager.Model, lengthUnit);
                        model.Elements.Columns = columnExporter.Export();

                        // Extract braces
                        BraceExport braceExporter = new BraceExport(modelManager.Model, lengthUnit);
                        model.Elements.Braces = braceExporter.Export();

                        // Extract isolated footings
                        IsolatedFootingExport isolatedFootingExporter = new IsolatedFootingExport(
                            modelManager.Model,
                            _materialProvider,
                            lengthUnit);
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

        // Extract diaphragm properties
        private List<Diaphragm> ExtractDiaphragms()
        {
            var diaphragms = new List<Diaphragm>();

            // Add a default rigid diaphragm
            var rigidDiaphragm = new Diaphragm
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.DIAPHRAGM),
                Name = "D1",
                Type = DiaphragmType.Rigid
            };

            diaphragms.Add(rigidDiaphragm);

            return diaphragms;
        }
    }
}
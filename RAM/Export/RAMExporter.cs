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

                    try
                    {
                        // Extract floor types
                        FloorTypeExport floorTypeExporter = new FloorTypeExport(modelManager.Model);
                        model.ModelLayout.FloorTypes = floorTypeExporter.Export();

                        // Create a mapping from RAM floor type UIDs to Core model IDs
                        Dictionary<int, string> floorTypeMapping = floorTypeExporter.CreateFloorTypeMapping(model.ModelLayout.FloorTypes);

                        // Extract levels
                        LevelExport levelExporter = new LevelExport(modelManager.Model, lengthUnit);
                        levelExporter.SetFloorTypeMapping(floorTypeMapping);
                        model.ModelLayout.Levels = levelExporter.Export();

                        // Create a mapping from level IDs to RAM story UIDs
                        Dictionary<string, string> levelMapping = levelExporter.CreateLevelMapping(model.ModelLayout.Levels);

                        // Extract grids
                        GridExport gridExporter = new GridExport(modelManager.Model, lengthUnit);
                        model.ModelLayout.Grids = gridExporter.Export();

                        // Extract materials
                        model.Properties.Materials = ExtractMaterials();

                        // Extract floor properties
                        //FloorPropertiesExport floorPropertiesExporter = new FloorPropertiesExport(modelManager.Model, lengthUnit);
                        //model.Properties.FloorProperties = floorPropertiesExporter.Export();

                        // Create material mappings
                        Dictionary<string, string> steelMaterialMapping = new Dictionary<string, string>();
                        Dictionary<string, string> concreteMaterialMapping = new Dictionary<string, string>();

                        foreach (var material in model.Properties.Materials)
                        {
                            if (material.Type.ToLower() == "steel")
                            {
                                steelMaterialMapping[material.Name] = material.Id;
                            }
                            else if (material.Type.ToLower() == "concrete")
                            {
                                concreteMaterialMapping[material.Name] = material.Id;
                            }
                        }


                        // Extract frame properties
                        var framePropertiesExporter = new FramePropertiesExport(modelManager.Model, lengthUnit);
                        var (frameProps, framePropMappings) = framePropertiesExporter.Export(model.Properties.Materials);
                        model.Properties.FrameProperties = frameProps;

                        // Extract wall properties
                        //model.Properties.WallProperties = ExtractWallProperties(model.Properties.Materials);

                        // Create wall property mappings
                        Dictionary<string, string> wallPropertyMapping = new Dictionary<string, string>();
                        foreach (var wallProp in model.Properties.WallProperties)
                        {
                            wallPropertyMapping[wallProp.Name] = wallProp.Id;
                        }

                        // Extract beams
                        BeamExport beamExporter = new BeamExport(modelManager.Model, lengthUnit);
                        beamExporter.SetLevelMappings(levelMapping);
                        beamExporter.SetFramePropertyMappings(framePropMappings);
                        model.Elements.Beams = beamExporter.Export();

                        // Extract columns
                        ColumnExport columnExporter = new ColumnExport(modelManager.Model, lengthUnit);
                        columnExporter.SetLevelMappings(levelMapping);
                        columnExporter.SetFramePropertyMappings(framePropMappings);
                        model.Elements.Columns = columnExporter.Export();

                        // Extract walls
                        WallExport wallExporter = new WallExport(modelManager.Model, lengthUnit);
                        wallExporter.SetLevelMappings(CreateLevelIdMapping(model.ModelLayout.Levels));
                        wallExporter.SetWallPropertyMappings(wallPropertyMapping);
                        model.Elements.Walls = wallExporter.Export();

                        // Extract isolated footings
                        IsolatedFootingExport isolatedFootingExporter = new IsolatedFootingExport(modelManager.Model, lengthUnit);
                        isolatedFootingExporter.SetLevelMappings(CreateLevelIdMapping(model.ModelLayout.Levels));
                        model.Elements.IsolatedFootings = isolatedFootingExporter.Export();

                        // Extract loads
                        //ExtractLoads(modelManager.Model, model);
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

        // Extract wall properties
        private List<WallProperties> ExtractWallProperties(List<Material> materials)
        {
            var wallProperties = new List<WallProperties>();

            // Find concrete material ID
            string concreteMaterialId = materials.FirstOrDefault(m => m.Type.ToLower() == "concrete")?.Id;
            if (string.IsNullOrEmpty(concreteMaterialId))
            {
                // If no concrete material found, add a default one
                var concreteMaterial = new Material
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                    Name = "Default Concrete",
                    Type = "Concrete"
                };
                materials.Add(concreteMaterial);
                concreteMaterialId = concreteMaterial.Id;
            }

            // Add default wall property
            wallProperties.Add(new WallProperties
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.WALL_PROPERTIES),
                Name = "SPEED Wall",
                MaterialId = concreteMaterialId,
                Thickness = 10.0
            });

            return wallProperties;
        }

        // Extract loads
        //private void ExtractLoads(IModel ramModel, BaseModel model)
        //{
        //    try
        //    {
        //        // Extract load definitions
        //        var deadLoad = new LoadDefinition
        //        {
        //            Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
        //            Name = "SW",
        //            Type = "Dead",
        //            SelfWeight = 1.0
        //        };

        //        var liveLoad = new LoadDefinition
        //        {
        //            Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION),
        //            Name = "Live",
        //            Type = "Live",
        //            SelfWeight = 0.0
        //        };

        //        model.Loads.LoadDefinitions.Add(deadLoad);
        //        model.Loads.LoadDefinitions.Add(liveLoad);

        //        // Extract surface loads
        //        SurfaceLoadExport surfaceLoadExporter = new SurfaceLoadExport(ramModel);

        //        // Create load definition mappings
        //        Dictionary<string, string> deadLoadMappings = new Dictionary<string, string>
        //        {
        //            ["default"] = deadLoad.Id
        //        };

        //        Dictionary<string, string> liveLoadMappings = new Dictionary<string, string>
        //        {
        //            ["default"] = liveLoad.Id
        //        };

        //        // Create floor type mappings
        //        Dictionary<string, string> floorTypeMappings = new Dictionary<string, string>();
        //        if (model.ModelLayout.FloorTypes.Count > 0)
        //        {
        //            floorTypeMappings["default"] = model.ModelLayout.FloorTypes[0].Id;
        //        }

        //        surfaceLoadExporter.SetLoadMappings(deadLoadMappings, liveLoadMappings);
        //        surfaceLoadExporter.SetFloorTypeMappings(floorTypeMappings);

        //        model.Loads.SurfaceLoads = surfaceLoadExporter.Export();

        //        // Create a default load combination
        //        var loadCombo = new LoadCombination
        //        {
        //            Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_COMBINATION),
        //            LoadDefinitionIds = new List<string> { deadLoad.Id, liveLoad.Id }
        //        };

        //        model.Loads.LoadCombinations.Add(loadCombo);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error extracting loads: {ex.Message}");
        //    }
        //}

        // Helper method to create a mapping from level IDs to their names
        // Helper method to create a mapping from level IDs to their names
        private Dictionary<string, string> CreateLevelIdMapping(List<Level> levels)
        {
            var mapping = new Dictionary<string, string>();

            foreach (var level in levels)
            {
                if (!string.IsNullOrEmpty(level.Id) && !string.IsNullOrEmpty(level.Name))
                {
                    // Map ID to name (for export classes to reference)
                    mapping[level.Id] = level.Name;

                    // Also map name to ID (for finding level by name)
                    mapping[level.Name] = level.Id;

                    // Map with "Story" prefix variations for flexibility
                    mapping[$"Story {level.Name}"] = level.Id;
                    mapping[$"Story{level.Name}"] = level.Id;
                }
            }

            return mapping;
        }

        #endregion
    }
}
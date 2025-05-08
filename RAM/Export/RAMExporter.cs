using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.ModelLayout;
using Core.Models.Elements;
using Core.Models.Properties;
using Core.Models.Properties.Materials;
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
using Core.Models.Properties.Floors;

namespace RAM
{
    public class RAMExporter
    {
        // Material management
        private List<Material> _materials = new List<Material>();
        private Dictionary<int, string> _ramToCoreMaterialMap = new Dictionary<int, string>();

        // Main method to convert RAM model to JSON
        public (string JsonOutput, string Message, bool Success) ConvertRAMToJSON(string ramFilePath)
        {
            try
            {
                // Reset collections
                _materials.Clear();
                _ramToCoreMaterialMap.Clear();

                // Open the RAM model
                using (var modelManager = new RAMModelManager())
                {
                    bool isOpen = modelManager.OpenModel(ramFilePath);
                    if (!isOpen)
                    {
                        return (null, "Failed to open RAM model file.", false);
                    }

                    // Add standard materials
                    AddStandardMaterials();

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

                        // Initialize mappings using the ModelMappingUtility
                        ModelMappingUtility.InitializeMappings(modelManager.Model, model);

                        // Extract frame properties
                        var framePropertiesExporter = new FramePropertiesExport(modelManager.Model, this, lengthUnit);
                        var frameProps = framePropertiesExporter.Export();
                        model.Properties.FrameProperties = frameProps;

                        // Extract floor properties
                        var floorPropertiesExporter = new FloorPropertiesExport(modelManager.Model, this, lengthUnit);
                        model.Properties.FloorProperties = floorPropertiesExporter.Export();
                        Console.WriteLine($"Exported {model.Properties.FloorProperties.Count} floor properties");

                        // Extract diaphragm properties
                        model.Properties.Diaphragms = ExtractDiaphragms();

                        // Extract floors
                        FloorExport floorExporter = new FloorExport(modelManager.Model, lengthUnit);
                        model.Elements.Floors = floorExporter.Export();
                        Console.WriteLine($"Exported {model.Elements.Floors.Count} floors");

                        // Extract wall properties before walls
                        var wallPropertiesExporter = new WallPropertiesExport(modelManager.Model, this, lengthUnit);
                        model.Properties.WallProperties = wallPropertiesExporter.Export();

                        // Update model with extracted materials
                        model.Properties.Materials = _materials;

                        // Extract structural elements
                        BeamExport beamExporter = new BeamExport(modelManager.Model, lengthUnit);
                        model.Elements.Beams = beamExporter.Export();

                        // Extract walls after wall properties
                        WallExport wallExporter = new WallExport(modelManager.Model, lengthUnit);
                        model.Elements.Walls = wallExporter.Export();

                        // Extract columns using the mapping utility
                        ColumnExport columnExporter = new ColumnExport(modelManager.Model, lengthUnit);
                        model.Elements.Columns = columnExporter.Export();

                        // Extract braces
                        BraceExport braceExporter = new BraceExport(modelManager.Model, lengthUnit);
                        model.Elements.Braces = braceExporter.Export();

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

        // Material management methods
        public List<Material> GetMaterials() => _materials;

        public Dictionary<int, string> GetMaterialMapping() => _ramToCoreMaterialMap;

        // Get or create a material based on RAM material ID and type
        public string GetOrCreateMaterialId(int ramMaterialId, EMATERIALTYPES materialType, IModel model)
        {
            // Check if already processed
            if (_ramToCoreMaterialMap.TryGetValue(ramMaterialId, out string existingId))
                return existingId;

            Material newMaterial = null;

            try
            {
                // Create material based on type
                if (materialType == EMATERIALTYPES.EConcreteMat)
                {
                    IConcreteMaterial concMaterial = model.GetConcreteMaterial(ramMaterialId);
                    if (concMaterial != null)
                    {
                        newMaterial = CreateConcreteMaterial(concMaterial);
                    }
                }
                else if (materialType == EMATERIALTYPES.ESteelMat || materialType == EMATERIALTYPES.ESteelJoistMat)
                {
                    ISteelMaterial steelMaterial = model.GetSteelMaterial(ramMaterialId);
                    if (steelMaterial != null)
                    {
                        newMaterial = CreateSteelMaterial(steelMaterial);
                    }
                }

                // If material creation succeeded
                if (newMaterial != null)
                {
                    _materials.Add(newMaterial);
                    _ramToCoreMaterialMap[ramMaterialId] = newMaterial.Id;
                    return newMaterial.Id;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating material from RAM ID {ramMaterialId}: {ex.Message}");
            }

            // Return default material ID as fallback
            if (materialType == EMATERIALTYPES.EConcreteMat)
                return GetDefaultConcreteMaterialId();
            else
                return GetDefaultSteelMaterialId();
        }

        // Create a concrete material from RAM data
        private Material CreateConcreteMaterial(IConcreteMaterial concMaterial)
        {
            if (concMaterial == null)
                return null;

            // Extract material properties
            double fc = concMaterial.dFc;
            double e = concMaterial.dE;
            double poissonsRatio = concMaterial.dPoissonsRatio;
            double weightDensity = concMaterial.dUnitWt;
            string name = concMaterial.strLabel;

            if (string.IsNullOrEmpty(name))
                name = $"{fc} psi Concrete";

            // Create material
            var material = new Material
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                Name = name,
                Type = "Concrete",
                DesignCodeId = "ACI318-19"
            };

            material.ConcreteProps = new ConcreteProperties
            {
                Fc = fc,
                ElasticModulus = e,
                WeightDensity = weightDensity,
                PoissonsRatio = poissonsRatio,
                WeightClass = DetermineConcreteWeightClass(weightDensity),
                ShearStrengthReductionFactor = 0.75 // Default
            };

            return material;
        }

        // Create a steel material from RAM data
        private Material CreateSteelMaterial(ISteelMaterial steelMaterial)
        {
            if (steelMaterial == null)
                return null;

            // Extract material properties
            double fy = steelMaterial.dFy;
            double fu = steelMaterial.dFu;
            double e = steelMaterial.dE;
            double poissonsRatio = steelMaterial.dPoissonsRatio;
            double weightDensity = steelMaterial.dUnitWt;
            string name = steelMaterial.strLabel;
            string test = steelMaterial.

            if (string.IsNullOrEmpty(name))
                name = $"{fy} ksi Steel";

            // Create material
            var material = new Material
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                Name = name,
                Type = "Steel",
                DesignCodeId = "AISC360-16"
            };

            material.SteelProps = new SteelProperties
            {
                Fy = fy,
                Fu = fu,
                ElasticModulus = e,
                WeightDensity = weightDensity,
                PoissonsRatio = poissonsRatio,
                Grade = DetermineSteelGrade(fy)
            };

            return material;
        }

        // Helper methods for material properties
        private string DetermineConcreteWeightClass(double weightDensity)
        {
            if (weightDensity < 115)
                return "Lightweight";
            else if (weightDensity > 115 && weightDensity < 140)
                return "SemiLightweight";
            else
                return "Normal";
        }

        private string DetermineSteelGrade(double fy)
        {
            if (Math.Abs(fy - 36000.0) < 1000.0)
                return "A36";
            else if (Math.Abs(fy - 50000.0) < 1000.0)
                return "A992";
            else if (Math.Abs(fy - 46000.0) < 1000.0)
                return "A572 Gr.50";
            else
                return "Unknown";
        }

        // Add standard materials to the material list
        private void AddStandardMaterials()
        {
            // Add standard steel material
            var steelMaterial = new Material
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                Name = "A992 Steel",
                Type = "Steel",
                DesignCodeId = "AISC360-16"
            };

            steelMaterial.SteelProps = new SteelProperties
            {
                Fy = 50000.0,                 // psi
                Fu = 65000.0,                 // psi
                ElasticModulus = 29000000.0,  // psi
                WeightDensity = 490.0,        // pcf
                PoissonsRatio = 0.3,
                Grade = "A992"
            };

            _materials.Add(steelMaterial);

            // Add standard concrete material
            var concreteMaterial = new Material
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                Name = "4000 psi Concrete",
                Type = "Concrete",
                DesignCodeId = "ACI318-19"
            };

            concreteMaterial.ConcreteProps = new ConcreteProperties
            {
                Fc = 4000.0,                  // psi
                ElasticModulus = 3600000.0,   // psi
                WeightDensity = 150.0,        // pcf
                PoissonsRatio = 0.2,
                WeightClass = "Normal",
                ShearStrengthReductionFactor = 0.75
            };

            _materials.Add(concreteMaterial);
        }

        // Get default material IDs
        public string GetDefaultConcreteMaterialId()
        {
            Material concrete = _materials.FirstOrDefault(m => m.Type == "Concrete");
            return concrete?.Id ?? IdGenerator.Generate(IdGenerator.Properties.MATERIAL);
        }

        public string GetDefaultSteelMaterialId()
        {
            Material steel = _materials.FirstOrDefault(m => m.Type == "Steel");
            return steel?.Id ?? IdGenerator.Generate(IdGenerator.Properties.MATERIAL);
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

        // Extract diaphragm properties
        private List<Diaphragm> ExtractDiaphragms()
        {
            var diaphragms = new List<Diaphragm>();

            // Add a default rigid diaphragm
            diaphragms.Add(new Diaphragm
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.DIAPHRAGM),
                Name = "Rigid Diaphragm",
                Type = "Rigid",
                StiffnessFactor = 1.0,
                MassFactor = 1.0
            });

            // Add a semi-rigid diaphragm
            diaphragms.Add(new Diaphragm
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.DIAPHRAGM),
                Name = "Semi-Rigid Diaphragm",
                Type = "Semi-Rigid",
                StiffnessFactor = 0.5,
                MassFactor = 1.0
            });

            // Add a flexible diaphragm
            diaphragms.Add(new Diaphragm
            {
                Id = IdGenerator.Generate(IdGenerator.Properties.DIAPHRAGM),
                Name = "Flexible Diaphragm",
                Type = "Flexible",
                StiffnessFactor = 0.1,
                MassFactor = 1.0
            });

            return diaphragms;
        }

        #endregion
    }
}
// RAMImporter.cs - Main entry point for import operations
using System;
using System.Collections.Generic;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Metadata;
using Core.Models.Properties;
using Core.Models.Loads;
using RAM.Core.Models;
using RAM.Utilities;
using RAMDATAACCESSLib;
using JSON_Connectors.Connectors.RAM.Export;
using RAM.Export;

namespace RAM.Import
{
    public class ModelToRAM
    {
        private RamDataAccess1 _ramDataAccess;
        private IDBIO1 _database;
        private IModel _model;
        private Dictionary<int, string> _materialIdMap = new Dictionary<int, string>();
        private Dictionary<int, string> _floorTypeIdMap = new Dictionary<int, string>();
        private Dictionary<int, string> _floorPropertyIdMap = new Dictionary<int, string>();
        private Dictionary<int, string> _loadCaseIdMap = new Dictionary<int, string>();

        public ModelToRAM()
        {
            _ramDataAccess = new RamDataAccess1();
        }

        public BaseModel ImportModel(string filePath)
        {
            try
            {
                // Initialize database
                _database = _ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
                _database.LoadDataBase2(filePath, "1");
                _model = _ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;

                // Create a new base model
                BaseModel model = new BaseModel();
                model.Elements = new ElementContainer();
                model.ModelLayout = new ModelLayoutContainer();
                model.Properties = new PropertiesContainer();
                model.Loads = new LoadContainer();
                model.Metadata = new MetadataContainer();

                // Import model components
                ImportMetadata(model);
                ImportFloorTypes(model);
                ImportGrids(model);
                ImportStories(model);
                ImportMaterials(model);
                ImportProperties(model);
                ImportElements(model);
                ImportLoads(model);

                // Close database
                _database.CloseDatabase();

                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing from RAM: {ex.Message}");
                if (_database != null)
                {
                    try
                    {
                        _database.CloseDatabase();
                    }
                    catch { }
                }
                throw;
            }
        }

        private void ImportMetadata(BaseModel model)
        {
            var importer = new MetadataToRAM(_model);
            model.Metadata = importer.Import();
        }

        private void ImportFloorTypes(BaseModel model)
        {
            var importer = new FloorTypeToRAM(_model);
            model.ModelLayout.FloorTypes = importer.Import();

            // Build mapping of RAM floor type IDs to model floor type IDs
            _floorTypeIdMap.Clear();
            IFloorTypes ramFloorTypes = _model.GetFloorTypes();
            for (int i = 0; i < ramFloorTypes.GetCount(); i++)
            {
                IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                var floorType = model.ModelLayout.FloorTypes.Find(ft => ft.Name == ramFloorType.strLabel);
                if (floorType != null)
                {
                    _floorTypeIdMap[ramFloorType.lUID] = floorType.Id;
                }
            }
        }

        private void ImportGrids(BaseModel model)
        {
            var importer = new GridToRAM(_model);
            model.ModelLayout.Grids = importer.Import();
        }

        private void ImportStories(BaseModel model)
        {
            var importer = new StoryToRAM(_model);
            model.ModelLayout.Levels = importer.Import();

            // Update level-to-floor-type relationships
            IStories ramStories = _model.GetStories();
            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory ramStory = ramStories.GetAt(i);
                var level = model.ModelLayout.Levels.Find(lvl => lvl.Name == ramStory.strLabel);
                if (level != null && _floorTypeIdMap.ContainsKey(ramStory.lFloorTypeId))
                {
                    level.FloorTypeId = _floorTypeIdMap[ramStory.lFloorTypeId];
                }
            }
        }

        private void ImportMaterials(BaseModel model)
        {
            var importer = new MaterialToRAM(_model);
            model.Properties.Materials = importer.Import();

            // Build mapping of RAM material IDs to model material IDs
            _materialIdMap.Clear();
            ISteelMaterials steelMaterials = _model.GetSteelMaterials();
            for (int i = 0; i < steelMaterials.GetCount(); i++)
            {
                ISteelMaterial steelMaterial = steelMaterials.GetAt(i);
                var material = model.Properties.Materials.Find(m => m.Name == steelMaterial.strLabel);
                if (material != null)
                {
                    _materialIdMap[steelMaterial.lUID] = material.Id;
                }
            }

            IConcreteMaterials concreteMaterials = _model.GetConcreteMaterials();
            for (int i = 0; i < concreteMaterials.GetCount(); i++)
            {
                IConcreteMaterial concreteMaterial = concreteMaterials.GetAt(i);
                var material = model.Properties.Materials.Find(m => m.Name == concreteMaterial.strLabel);
                if (material != null)
                {
                    _materialIdMap[concreteMaterial.lUID] = material.Id;
                }
            }
        }

        private void ImportProperties(BaseModel model)
        {
            // Import various property types
            var slabImporter = new SlabPropertiesToRAM(_model, _materialIdMap);
            var compDeckImporter = new CompositeDeckPropertiesImporter(_model, _materialIdMap);
            var nonCompDeckImporter = new NonCompositeDeckPropertiesImporter(_model, _materialIdMap);
            var frameSectionImporter = new FrameSectionPropertiesToRAM(_model, _materialIdMap);

            // Combine all floor property types
            List<FloorProperties> floorProperties = new List<FloorProperties>();
            floorProperties.AddRange(slabImporter.Import());
            floorProperties.AddRange(compDeckImporter.Import());
            floorProperties.AddRange(nonCompDeckImporter.Import());
            model.Properties.FloorProperties = floorProperties;

            // Import frame properties
            model.Properties.FrameProperties = frameSectionImporter.Import();

            // Build mapping of property IDs
            _floorPropertyIdMap.Clear();

            // Map concrete slab properties
            IConcSlabProps concSlabProps = _model.GetConcreteSlabProps();
            for (int i = 0; i < concSlabProps.GetCount(); i++)
            {
                IConcSlabProp concSlabProp = concSlabProps.GetAt(i);
                var floorProp = model.Properties.FloorProperties.Find(fp => fp.Name == concSlabProp.strLabel);
                if (floorProp != null)
                {
                    _floorPropertyIdMap[concSlabProp.lUID] = floorProp.Id;
                }
            }

            // Map composite deck properties
            ICompDeckProps compDeckProps = _model.GetCompositeDeckProps();
            for (int i = 0; i < compDeckProps.GetCount(); i++)
            {
                ICompDeckProp compDeckProp = compDeckProps.GetAt(i);
                var floorProp = model.Properties.FloorProperties.Find(fp => fp.Name == compDeckProp.strLabel);
                if (floorProp != null)
                {
                    _floorPropertyIdMap[compDeckProp.lUID] = floorProp.Id;
                }
            }

            // Map non-composite deck properties
            INonCompDeckProps nonCompDeckProps = _model.GetNonCompDeckProps();
            for (int i = 0; i < nonCompDeckProps.GetCount(); i++)
            {
                INonCompDeckProp nonCompDeckProp = nonCompDeckProps.GetAt(i);
                var floorProp = model.Properties.FloorProperties.Find(fp => fp.Name == nonCompDeckProp.strLabel);
                if (floorProp != null)
                {
                    _floorPropertyIdMap[nonCompDeckProp.lUID] = floorProp.Id;
                }
            }

            // Import wall properties
            var wallPropertiesImporter = new WallPropertiesImporter(_model, _materialIdMap);
            model.Properties.WallProperties = wallPropertiesImporter.Import();

            // Import diaphragm properties
            var diaphragmImporter = new DiaphragmImporter(_model);
            model.Properties.Diaphragms = diaphragmImporter.Import();
        }

        private void ImportElements(BaseModel model)
        {
            // Import beams
            var beamImporter = new BeamImporter(_model, _floorTypeIdMap, _materialIdMap);
            model.Elements.Beams = beamImporter.Import();

            // Import columns
            var columnImporter = new ColumnImporter(_model, _floorTypeIdMap, _materialIdMap);
            model.Elements.Columns = columnImporter.Import();

            // Import walls
            var wallImporter = new WallImporter(_model, _floorTypeIdMap, _materialIdMap);
            model.Elements.Walls = wallImporter.Import();

            // Import floors
            var floorImporter = new FloorToRAM(_model, _floorTypeIdMap, _floorPropertyIdMap);
            model.Elements.Floors = floorImporter.Import();
        }

        private void ImportLoads(BaseModel model)
        {
            // Import load definitions
            var loadDefinitionImporter = new LoadDefinitionToRAM(_model);
            model.Loads.LoadDefinitions = loadDefinitionImporter.Import();

            // Build mapping of RAM load case IDs to model load definition IDs
            _loadCaseIdMap.Clear();
            ILoadCases ramLoadCases = _model.GetLoadCases();
            for (int i = 0; i < ramLoadCases.GetCount(); i++)
            {
                ILoadCase ramLoadCase = ramLoadCases.GetAt(i);
                var loadDef = model.Loads.LoadDefinitions.Find(ld => ld.Name == ramLoadCase.strLabel);
                if (loadDef != null)
                {
                    _loadCaseIdMap[ramLoadCase.lUID] = loadDef.Id;
                }
            }

            // Import load combinations
            var loadCombinationImporter = new LoadCombinationToRAM(_model, _loadCaseIdMap);
            model.Loads.LoadCombinations = loadCombinationImporter.Import();

            // Import surface loads
            var surfaceLoadImporter = new SurfaceLoadToRAM(_model, _loadCaseIdMap);
            model.Loads.SurfaceLoads = surfaceLoadImporter.Import();
        }
    }
}
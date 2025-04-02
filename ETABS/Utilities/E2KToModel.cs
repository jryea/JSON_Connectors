using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Loads;
using Core.Models.Metadata;
using ETABS.Import.Elements;
using ETABS.Import.Loads;
using ETABS.Import.Metadata;
using ETABS.Import.ModelLayout;
using ETABS.Import.Properties;
using System;
using System.Collections.Generic;

namespace ETABS.Utilities
{
    /// <summary>
    /// Main importer class for converting ETABS E2K format to model
    /// </summary>
    public class E2KToModel
    {
        // Element importers
        private readonly ElementsImporter _elementsImporter = new ElementsImporter();

        // Other importers (stubs for now, would need to be implemented)
        private readonly MaterialsImport _materialsImport = new MaterialsImport();
        private readonly FramePropertiesImport _framePropertiesImport = new FramePropertiesImport();
        private readonly FloorPropertiesImport _floorPropertiesImport = new FloorPropertiesImport();
        private readonly StoriesImport _storiesImport = new StoriesImport();
        private readonly GridsImport _gridsImport = new GridsImport();
        private readonly DiaphragmsImport _diaphragmsImport = new DiaphragmsImport();
        private readonly ProjectInfoImport _projectInfoImport = new ProjectInfoImport();
        private readonly UnitsImport _unitsImport = new UnitsImport();
        private readonly LoadDefinitionsImport _loadDefinitionsImport = new LoadDefinitionsImport();
        private readonly SurfaceLoadsImport _surfaceLoadsImport = new SurfaceLoadsImport();
        private readonly LoadCombinationsImport _loadCombinationsImport = new LoadCombinationsImport();

        /// <summary>
        /// Imports a model from E2K sections
        /// </summary>
        /// <param name="e2kSections">Dictionary of E2K sections</param>
        /// <returns>BaseModel with imported data</returns>
        public BaseModel ImportFromE2K(Dictionary<string, string> e2kSections)
        {
            try
            {
                BaseModel model = new BaseModel();

                // Initialize metadata
                model.Metadata = new MetadataContainer();

                // Parse project info and units
                if (e2kSections.TryGetValue("PROJECT INFORMATION", out string projectInfoSection))
                {
                    model.Metadata.ProjectInfo = ImportProjectInfo(projectInfoSection);
                }

                if (e2kSections.TryGetValue("CONTROLS", out string controlsSection))
                {
                    model.Metadata.Units = ImportUnits(controlsSection);
                }

                // Initialize model layout container
                model.ModelLayout = new ModelLayoutContainer();

                // Parse stories/levels
                if (e2kSections.TryGetValue("STORIES - IN SEQUENCE FROM TOP", out string storiesSection))
                {
                    model.ModelLayout.Levels = ImportLevels(storiesSection);
                }

                // Parse grids
                if (e2kSections.TryGetValue("GRIDS", out string gridsSection))
                {
                    model.ModelLayout.Grids = ImportGrids(gridsSection);
                }

                // Initialize properties container
                model.Properties = new PropertiesContainer();

                // Parse materials
                if (e2kSections.TryGetValue("MATERIAL PROPERTIES", out string materialsSection))
                {
                    model.Properties.Materials = ImportMaterials(materialsSection);
                }

                // Parse frame properties
                if (e2kSections.TryGetValue("FRAME SECTIONS", out string frameSectionsSection))
                {
                    model.Properties.FrameProperties = ImportFrameProperties(frameSectionsSection);
                }

                // Parse wall properties
                if (e2kSections.TryGetValue("WALL PROPERTIES", out string wallPropertiesSection))
                {
                    model.Properties.WallProperties = ImportWallProperties(wallPropertiesSection);
                }

                // Parse floor properties
                if (e2kSections.ContainsKey("SLAB PROPERTIES") || e2kSections.ContainsKey("DECK PROPERTIES"))
                {
                    string slabSection = e2kSections.GetValueOrDefault("SLAB PROPERTIES", "");
                    string deckSection = e2kSections.GetValueOrDefault("DECK PROPERTIES", "");
                    model.Properties.FloorProperties = ImportFloorProperties(slabSection, deckSection);
                }

                // Parse diaphragms
                if (e2kSections.TryGetValue("DIAPHRAGM NAMES", out string diaphragmsSection))
                {
                    model.Properties.Diaphragms = ImportDiaphragms(diaphragmsSection);
                }

                // Initialize loads container
                model.Loads = new LoadContainer();

                // Parse load definitions
                if (e2kSections.TryGetValue("LOAD PATTERNS", out string loadPatternsSection))
                {
                    model.Loads.LoadDefinitions = ImportLoadDefinitions(loadPatternsSection);
                }

                // Parse load combinations
                if (e2kSections.TryGetValue("LOAD COMBINATIONS", out string loadCombosSection))
                {
                    model.Loads.LoadCombinations = ImportLoadCombinations(loadCombosSection);
                }

                // Parse surface loads
                if (e2kSections.TryGetValue("SHELL UNIFORM LOAD SETS", out string surfaceLoadsSection))
                {
                    model.Loads.SurfaceLoads = ImportSurfaceLoads(surfaceLoadsSection);
                }

                // Parse and set up elements
                _elementsImporter.ParseE2KSections(e2kSections);
                _elementsImporter.SetupReferences(
                    model.ModelLayout.Levels,
                    model.Properties.FrameProperties,
                    model.Properties.FloorProperties,
                    model.Properties.WallProperties,
                    model.Properties.Diaphragms);

                // Import elements
                model.Elements = _elementsImporter.ImportElements();

                return model;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error importing from E2K: {ex.Message}", ex);
            }
        }

        #region Import Methods

        // Import methods using the specialized import classes
        // These would need to be fully implemented to handle all E2K format variations

        private ProjectInfo ImportProjectInfo(string projectInfoSection)
        {
            // Placeholder - would use _projectInfoImport
            return new ProjectInfo
            {
                ProjectName = "Imported from E2K",
                ProjectId = Guid.NewGuid().ToString(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };
        }

        private Units ImportUnits(string controlsSection)
        {
            // Placeholder - would use _unitsImport
            return new Units
            {
                Length = "inches",
                Force = "pounds",
                Temperature = "fahrenheit"
            };
        }

        private List<Level> ImportLevels(string storiesSection)
        {
            // Placeholder - would use _storiesImport
            return new List<Level>();
        }

        private List<Grid> ImportGrids(string gridsSection)
        {
            // Placeholder - would use _gridsImport
            return new List<Grid>();
        }

        private List<Material> ImportMaterials(string materialsSection)
        {
            // Placeholder - would use _materialsImport
            return new List<Material>();
        }

        private List<FrameProperties> ImportFrameProperties(string frameSectionsSection)
        {
            // Placeholder - would use _framePropertiesImport
            return new List<FrameProperties>();
        }

        private List<WallProperties> ImportWallProperties(string wallPropertiesSection)
        {
            // Placeholder - would implement a wall properties importer
            return new List<WallProperties>();
        }

        private List<FloorProperties> ImportFloorProperties(string slabSection, string deckSection)
        {
            // Placeholder - would use _floorPropertiesImport
            return new List<FloorProperties>();
        }

        private List<Diaphragm> ImportDiaphragms(string diaphragmsSection)
        {
            // Placeholder - would use _diaphragmsImport
            return new List<Diaphragm>();
        }

        private List<LoadDefinition> ImportLoadDefinitions(string loadPatternsSection)
        {
            // Placeholder - would use _loadDefinitionsImport
            return new List<LoadDefinition>();
        }

        private List<LoadCombination> ImportLoadCombinations(string loadCombosSection)
        {
            // Placeholder - would use _loadCombinationsImport
            return new List<LoadCombination>();
        }

        private List<SurfaceLoad> ImportSurfaceLoads(string surfaceLoadsSection)
        {
            // Placeholder - would use _surfaceLoadsImport
            return new List<SurfaceLoad>();
        }

        #endregion
    }
}
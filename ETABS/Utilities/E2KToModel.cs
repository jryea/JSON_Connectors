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
using ETABS.Import.Utilities;
using System;
using System.Collections.Generic;

namespace ETABS.Utilities
{
    // Main importer class for converting ETABS E2K format to model
    public class E2KToModel
    {
        // Element importers
        private readonly ElementsImporter _elementsImporter = new ElementsImporter();

        // Utility parsers
        private readonly PointsCollector _pointsCollector = new PointsCollector();
        private readonly AreaParser _areaParser = new AreaParser();
        private readonly LineConnectivityParser _lineConnectivityParser = new LineConnectivityParser();
        private readonly LineAssignmentParser _lineAssignmentParser = new LineAssignmentParser();

        // Property and metadata importers
        private readonly MaterialsImport _materialsImport = new MaterialsImport();
        private readonly FramePropertiesImport _framePropertiesImport = new FramePropertiesImport();
        private readonly FloorPropertiesImport _floorPropertiesImport = new FloorPropertiesImport();
        private readonly WallPropertiesImport _wallPropertiesImport = new WallPropertiesImport();
        private readonly DiaphragmsImport _diaphragmsImport = new DiaphragmsImport();
        private readonly StoriesImport _storiesImport = new StoriesImport();
        private readonly GridsImport _gridsImport;
        private readonly ProjectInfoImport _projectInfoImport = new ProjectInfoImport();
        private readonly UnitsImport _unitsImport = new UnitsImport();

        // Load importers
        private readonly LoadDefinitionsImport _loadDefinitionsImport = new LoadDefinitionsImport();
        private readonly SurfaceLoadsImport _surfaceLoadsImport = new SurfaceLoadsImport();
        private readonly LoadCombinationsImport _loadCombinationsImport = new LoadCombinationsImport();

        // Initializes a new instance of the E2KToModel class
        public E2KToModel()
        {
            // Initialize GridsImport with the PointsCollector
            _gridsImport = new GridsImport(_pointsCollector);
        }

        
        // Imports a model from E2K sections
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
                    model.Metadata.ProjectInfo = ImportProjectInfo(projectInfoSection, e2kSections);
                }
                else
                {
                    model.Metadata.ProjectInfo = new ProjectInfo
                    {
                        ProjectName = "Imported from E2K",
                        ProjectId = Guid.NewGuid().ToString(),
                        CreationDate = DateTime.Now,
                        SchemaVersion = "1.0"
                    };
                }

                if (e2kSections.TryGetValue("CONTROLS", out string controlsSection))
                {
                    model.Metadata.Units = ImportUnits(controlsSection);
                }
                else
                {
                    model.Metadata.Units = new Units
                    {
                        Length = "inches",
                        Force = "pounds",
                        Temperature = "fahrenheit"
                    };
                }

                // Initialize model layout container
                model.ModelLayout = new ModelLayoutContainer();

                // First, we need to parse all the points for later use
                if (e2kSections.TryGetValue("POINT COORDINATES", out string pointsSection))
                {
                    _pointsCollector.ParsePoints(pointsSection);
                }

                // Parse stories/levels
                if (e2kSections.TryGetValue("STORIES - IN SEQUENCE FROM TOP", out string storiesSection))
                {
                    model.ModelLayout.Levels = ImportLevels(storiesSection);
                }

                // Create a default floor type if none exists
                if (model.ModelLayout.FloorTypes.Count == 0)
                {
                    model.ModelLayout.FloorTypes.Add(new FloorType
                    {
                        Name = "typical",
                        Description = "Default floor type"
                    });
                }

                // Set floor types for levels
                string defaultFloorTypeId = model.ModelLayout.FloorTypes.Count > 0 ? model.ModelLayout.FloorTypes[0].Id : null;
                Dictionary<string, string> floorTypeNames = new Dictionary<string, string>();
                foreach (var floorType in model.ModelLayout.FloorTypes)
                {
                    floorTypeNames[floorType.Name] = floorType.Id;
                }
                _storiesImport.SetFloorTypes(floorTypeNames);

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

                // Set materials for other property importers
                _framePropertiesImport.SetMaterials(model.Properties.Materials);
                _floorPropertiesImport.SetMaterials(model.Properties.Materials);
                _wallPropertiesImport.SetMaterials(model.Properties.Materials);

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
                    string slabSection = e2kSections.TryGetValue("SLAB PROPERTIES", out string slabValue) ? slabValue : "";
                    string deckSection = e2kSections.TryGetValue("DECK PROPERTIES", out string deckValue) ? deckValue : "";
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

                // Set load definitions for other load importers
                _surfaceLoadsImport.SetLoadDefinitions(model.Loads.LoadDefinitions);

                // Parse load combinations
                if (e2kSections.TryGetValue("LOAD COMBINATIONS", out string loadCombosSection))
                {
                    model.Loads.LoadCombinations = ImportLoadCombinations(loadCombosSection, model.Loads.LoadDefinitions);
                }

                // Parse surface loads
                if (e2kSections.TryGetValue("SHELL UNIFORM LOAD SETS", out string shellUniformLoadSetsSection))
                {
                    string shellObjectLoadsSection = e2kSections.TryGetValue("SHELL OBJECT LOADS", out string loadsValue) ? loadsValue : "";
                    _surfaceLoadsImport.SetFloorTypes(floorTypeNames);
                    model.Loads.SurfaceLoads = ImportSurfaceLoads(shellUniformLoadSetsSection, shellObjectLoadsSection);
                }

                // Parse line connectivities and assignments
                if (e2kSections.TryGetValue("LINE CONNECTIVITIES", out string lineConnectivitiesSection))
                {
                    _lineConnectivityParser.ParseLineConnectivities(lineConnectivitiesSection);
                }

                if (e2kSections.TryGetValue("LINE ASSIGNS", out string lineAssignsSection))
                {
                    _lineAssignmentParser.ParseLineAssignments(lineAssignsSection);
                }

                // Parse area connectivities and assignments
                if (e2kSections.TryGetValue("AREA CONNECTIVITIES", out string areaConnectivitiesSection))
                {
                    _areaParser.ParseAreaConnectivities(areaConnectivitiesSection);
                }

                if (e2kSections.TryGetValue("AREA ASSIGNS", out string areaAssignsSection))
                {
                    _areaParser.ParseAreaAssignments(areaAssignsSection);
                }

                // Set up element importer references
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

        // Imports project information from E2K sections
        private ProjectInfo ImportProjectInfo(string projectInfoSection, Dictionary<string, string> e2kSections)
        {
            ProjectInfo projectInfo = _projectInfoImport.Import(projectInfoSection);

            // Extract additional information from other sections
            if (e2kSections.TryGetValue("PROGRAM INFORMATION", out string programInfoSection))
            {
                if (e2kSections.TryGetValue("LOG", out string logSection))
                {
                    _projectInfoImport.ExtractAdditionalInfo(programInfoSection, logSection, projectInfo);
                }
            }

            if (e2kSections.TryGetValue("CONTROLS", out string controlsSection))
            {
                _projectInfoImport.ExtractFromControls(controlsSection, projectInfo);
            }

            return projectInfo;
        }

        // Imports units from E2K CONTROLS section
        private Units ImportUnits(string controlsSection)
        {
            return _unitsImport.Import(controlsSection);
        }

        /// Imports levels from E2K STORIES section
        private List<Level> ImportLevels(string storiesSection)
        {
            return _storiesImport.Import(storiesSection);
        }

        // Imports grids from E2K GRIDS section
        private List<Grid> ImportGrids(string gridsSection)
        {
            return _gridsImport.Import(gridsSection);
        }

        // Imports materials from E2K MATERIAL PROPERTIES section
        private List<Material> ImportMaterials(string materialsSection)
        {
            return _materialsImport.Import(materialsSection);
        }

        // Imports frame properties from E2K FRAME SECTIONS section
        private List<FrameProperties> ImportFrameProperties(string frameSectionsSection)
        {
            return _framePropertiesImport.Import(frameSectionsSection);
        }

        // Imports wall properties from E2K WALL PROPERTIES section
        private List<WallProperties> ImportWallProperties(string wallPropertiesSection)
        {
            return _wallPropertiesImport.Import(wallPropertiesSection);
        }

        // Imports floor properties from E2K SLAB PROPERTIES and DECK PROPERTIES sections
        private List<FloorProperties> ImportFloorProperties(string slabSection, string deckSection)
        {
            return _floorPropertiesImport.Import(slabSection, deckSection);
        }

        // Imports diaphragms from E2K DIAPHRAGM NAMES section
        private List<Diaphragm> ImportDiaphragms(string diaphragmsSection)
        {
            return _diaphragmsImport.Import(diaphragmsSection);
        }

        // Imports load definitions from E2K LOAD PATTERNS section
        private List<LoadDefinition> ImportLoadDefinitions(string loadPatternsSection)
        {
            return _loadDefinitionsImport.Import(loadPatternsSection);
        }

        // Imports load combinations from E2K LOAD COMBINATIONS section
        private List<LoadCombination> ImportLoadCombinations(string loadCombosSection, List<LoadDefinition> loadDefinitions)
        {
            // Need to implement the LoadCombinationsImport class properly
            // For now, return an empty list to avoid null reference errors
            return new List<LoadCombination>();
        }

        // Imports surface loads from E2K SHELL UNIFORM LOAD SETS and SHELL OBJECT LOADS sections
        private List<SurfaceLoad> ImportSurfaceLoads(string surfaceLoadsSection, string shellObjectLoadsSection)
        {
            return _surfaceLoadsImport.Import(surfaceLoadsSection, shellObjectLoadsSection);
        }

        #endregion
    }
}
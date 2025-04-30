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
using ETABS.Utilities;
using System;
using System.Collections.Generic;

namespace ETABS.FromETABS
{
    public class ETABSToModel
    {
        // Element importers
        private readonly ETABSToElements _elementsImporter = new ETABSToElements();

        // Utility parsers
        private readonly PointsCollector _pointsCollector = new PointsCollector();
        private readonly AreaParser _areaParser = new AreaParser();
        private readonly LineConnectivityParser _lineConnectivityParser = new LineConnectivityParser();
        private readonly LineAssignmentParser _lineAssignmentParser = new LineAssignmentParser();

        // Property and metadata importers
        private readonly ETABSToMaterial _materialsImporter = new ETABSToMaterial();
        private readonly ETABSToFrameProperties _framePropertiesImporter = new ETABSToFrameProperties();
        private readonly ETABSToFloorProperties _floorPropertiesImporter = new ETABSToFloorProperties();
        private readonly WallPropertiesImport _wallPropertiesImporter = new WallPropertiesImport();
        private readonly ETABSToDiaphragm _diaphragmsImporter = new ETABSToDiaphragm();
        private readonly ETABSToStory _storiesImporter = new ETABSToStory();
        private readonly ETABSToGrid _gridsImporter;
        private readonly ETABSToProjectInfo _projectInfoImporter = new ETABSToProjectInfo();
        private readonly ETABSToUnits _unitsImporter = new ETABSToUnits();

        // Load importers
        private readonly ETABSToLoadDefinition _loadDefinitionsImporter = new ETABSToLoadDefinition();
        private readonly ETABSToSurfaceLoad _surfaceLoadsImporter = new ETABSToSurfaceLoad();
        private readonly ETABSToLoadCombination _loadCombinationsImporter = new ETABSToLoadCombination();

        public ETABSToModel()
        {
            _gridsImporter = new ETABSToGrid(_pointsCollector);
        }

        public BaseModel ImportFromE2K(Dictionary<string, string> e2kSections)
        {
            try
            {
                BaseModel model = new BaseModel();

                // Parse project info and units
                string controlsSection = null;
                if (e2kSections.TryGetValue("CONTROLS", out controlsSection))
                {
                    model.Metadata.Units = _unitsImporter.Import(controlsSection);
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

                if (e2kSections.TryGetValue("PROJECT INFORMATION", out string projectInfoSection))
                {
                    model.Metadata.ProjectInfo = _projectInfoImporter.Import(projectInfoSection);

                    // Extract additional info from other sections
                    if (e2kSections.TryGetValue("PROGRAM INFORMATION", out string programInfoSection))
                    {
                        if (e2kSections.TryGetValue("LOG", out string logSection))
                        {
                            _projectInfoImporter.ExtractAdditionalInfo(programInfoSection, logSection, model.Metadata.ProjectInfo);
                        }
                    }

                    // Use the already obtained controlsSection without redeclaring it
                    if (controlsSection != null)
                    {
                        _projectInfoImporter.ExtractFromControls(controlsSection, model.Metadata.ProjectInfo);
                    }
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

                // Initialize model layout container
                model.ModelLayout = new ModelLayoutContainer();

                // Parse all points for later use
                if (e2kSections.TryGetValue("POINT COORDINATES", out string pointsSection))
                {
                    _pointsCollector.ParsePoints(pointsSection);
                }

                // Parse stories/levels
                if (e2kSections.TryGetValue("STORIES - IN SEQUENCE FROM TOP", out string storiesSection))
                {
                    // First generate all floor types
                    var floorTypeImporter = new ETABSToFloorType();
                    model.ModelLayout.FloorTypes = floorTypeImporter.Import(storiesSection);

                    // Pass the FloorTypeImporter to the storiesImporter to ensure consistent IDs
                    // This could be done by adding a method to ETABSToStory:
                    _storiesImporter.UseFloorTypeMapping(floorTypeImporter.GetFloorTypeMapping());

                    // Now import levels with correct FloorTypeIds
                    model.ModelLayout.Levels = _storiesImporter.Import(storiesSection);
                }

                // Parse grids
                if (e2kSections.TryGetValue("GRIDS", out string gridsSection))
                {
                    model.ModelLayout.Grids = _gridsImporter.Import(gridsSection);
                }

                // Initialize properties container
                model.Properties = new PropertiesContainer();

                // Parse materials
                if (e2kSections.TryGetValue("MATERIAL PROPERTIES", out string materialsSection))
                {
                    model.Properties.Materials = _materialsImporter.Import(materialsSection);

                    // Set materials for other property importers
                    _framePropertiesImporter.SetMaterials(model.Properties.Materials);
                    _floorPropertiesImporter.SetMaterials(model.Properties.Materials);
                    _wallPropertiesImporter.SetMaterials(model.Properties.Materials);
                }

                // Parse frame properties
                if (e2kSections.TryGetValue("FRAME SECTIONS", out string frameSectionsSection))
                {
                    model.Properties.FrameProperties = _framePropertiesImporter.Import(frameSectionsSection);
                }

                // Parse wall properties
                if (e2kSections.TryGetValue("WALL PROPERTIES", out string wallPropertiesSection))
                {
                    model.Properties.WallProperties = _wallPropertiesImporter.Import(wallPropertiesSection);
                }

                // Parse floor properties
                if (e2kSections.ContainsKey("SLAB PROPERTIES") || e2kSections.ContainsKey("DECK PROPERTIES"))
                {
                    string slabSection = e2kSections.TryGetValue("SLAB PROPERTIES", out string slabValue) ? slabValue : "";
                    string deckSection = e2kSections.TryGetValue("DECK PROPERTIES", out string deckValue) ? deckValue : "";
                    model.Properties.FloorProperties = _floorPropertiesImporter.Import(slabSection, deckSection);
                }

                // Parse diaphragms
                if (e2kSections.TryGetValue("DIAPHRAGM NAMES", out string diaphragmsSection))
                {
                    model.Properties.Diaphragms = _diaphragmsImporter.Import(diaphragmsSection);
                }

                // Initialize loads container
                model.Loads = new LoadContainer();

                // Parse load definitions
                if (e2kSections.TryGetValue("LOAD PATTERNS", out string loadPatternsSection))
                {
                    model.Loads.LoadDefinitions = _loadDefinitionsImporter.Import(loadPatternsSection);

                    // Set load definitions for other load importers
                    _surfaceLoadsImporter.SetLoadDefinitions(model.Loads.LoadDefinitions);
                    _loadCombinationsImporter.SetLoadDefinitions(model.Loads.LoadDefinitions);
                }

                // Parse load combinations
                if (e2kSections.TryGetValue("LOAD COMBINATIONS", out string loadCombosSection))
                {
                    model.Loads.LoadCombinations = _loadCombinationsImporter.Import(loadCombosSection, model.Loads.LoadDefinitions);
                }

                // Parse surface loads
                if (e2kSections.TryGetValue("SHELL UNIFORM LOAD SETS", out string shellUniformLoadSetsSection))
                {
                    string shellObjectLoadsSection = e2kSections.TryGetValue("SHELL OBJECT LOADS", out string loadsValue) ? loadsValue : "";
                    model.Loads.SurfaceLoads = _surfaceLoadsImporter.Import(shellUniformLoadSetsSection, shellObjectLoadsSection);
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
    }
}
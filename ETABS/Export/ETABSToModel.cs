using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Loads;
using Core.Models.Metadata;
using ETABS.Export.Elements;
using ETABS.Export.Loads;
using ETABS.Export.ModelLayout;
using ETABS.Export.Properties;
using ETABS.Utilities;
using System;
using System.Collections.Generic;
using ETABS.Export.Metadata;

namespace ETABS.Export
{
    public class ETABSToModel
    {
        // Element importers
        private readonly ElementsExport _elementsExporter = new ElementsExport();

        // Utility parsers
        private readonly PointsCollector _pointsCollector = new PointsCollector();  
        private readonly AreaParser _areaParser = new AreaParser();
        private readonly LineConnectivityParser _lineConnectivityParser = new LineConnectivityParser();
        private readonly LineAssignmentParser _lineAssignmentParser = new LineAssignmentParser();

        // Property and metadata importers
        private readonly MaterialExport _materialExporter = new MaterialExport();
        private readonly FramePropertiesExport _framePropertiesExporter = new FramePropertiesExport();
        private readonly FloorPropertiesExport _floorPropertiesExporter = new FloorPropertiesExport();
        private readonly WallPropertiesExport _wallPropertiesExporter = new WallPropertiesExport();
        private readonly DiaphragmExport _diaphragmsExporter = new DiaphragmExport();
        private readonly StoryExport _storiesImporter = new StoryExport();
        private readonly GridExport _gridsExporter = new GridExport();
        private readonly ProjectInfoExport _projectInfoExporter = new ProjectInfoExport();
        private readonly UnitsExport _unitsExporter = new UnitsExport();

        // Load importers
        private readonly LoadDefinitionExport _loadDefinitionsExporter = new LoadDefinitionExport();
        private readonly SurfaceLoadExport _surfaceLoadsExporter = new SurfaceLoadExport();
        private readonly LoadCombinationExport _loadCombinationsExporter = new LoadCombinationExport();

        public ETABSToModel()
        {
            _gridsExporter = new GridExport(_pointsCollector);
        }

        public BaseModel Export(Dictionary<string, string> e2kSections)
        {
            try
            {
                BaseModel model = new BaseModel();

                // Parse project info and units
                string controlsSection = null;
                if (e2kSections.TryGetValue("CONTROLS", out controlsSection))
                {
                    model.Metadata.Units = _unitsExporter.Import(controlsSection);
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
                    model.Metadata.ProjectInfo = _projectInfoExporter.Export(projectInfoSection);

                    // Extract additional info from other sections
                    if (e2kSections.TryGetValue("PROGRAM INFORMATION", out string programInfoSection))
                    {
                        if (e2kSections.TryGetValue("LOG", out string logSection))
                        {
                            _projectInfoExporter.ExtractAdditionalInfo(programInfoSection, logSection, model.Metadata.ProjectInfo);
                        }
                    }

                    // Use the already obtained controlsSection without redeclaring it
                    if (controlsSection != null)
                    {
                        _projectInfoExporter.ExtractFromControls(controlsSection, model.Metadata.ProjectInfo);
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
                    var floorTypeExporter = new FloorTypeExport();
                    model.ModelLayout.FloorTypes = floorTypeExporter.Export(storiesSection);

                    // Pass the FloorTypeImporter to the storiesImporter to ensure consistent IDs
                    // This could be done by adding a method to ETABSToStory:
                    _storiesImporter.UseFloorTypeMapping(floorTypeExporter.GetFloorTypeMapping());

                    // Now import levels with correct FloorTypeIds
                    model.ModelLayout.Levels = _storiesImporter.Import(storiesSection);
                }

                // Parse grids
                if (e2kSections.TryGetValue("GRIDS", out string gridsSection))
                {
                    model.ModelLayout.Grids = _gridsExporter.Export(gridsSection);
                }

                // Initialize properties container
                model.Properties = new PropertiesContainer();

                // Parse materials
                if (e2kSections.TryGetValue("MATERIAL PROPERTIES", out string materialsSection))
                {
                    model.Properties.Materials = _materialExporter.Export(materialsSection);

                    // Set materials for other property importers
                    _framePropertiesExporter.SetMaterials(model.Properties.Materials);
                    _floorPropertiesExporter.SetMaterials(model.Properties.Materials);
                    _wallPropertiesExporter.SetMaterials(model.Properties.Materials);
                }

                // Parse frame properties
                if (e2kSections.TryGetValue("FRAME SECTIONS", out string frameSectionsSection))
                {
                    model.Properties.FrameProperties = _framePropertiesExporter.Export(frameSectionsSection);
                }

                // Parse wall properties
                if (e2kSections.TryGetValue("WALL PROPERTIES", out string wallPropertiesSection))
                {
                    model.Properties.WallProperties = _wallPropertiesExporter.Export(wallPropertiesSection);
                }

                // Parse floor properties
                if (e2kSections.ContainsKey("SLAB PROPERTIES") || e2kSections.ContainsKey("DECK PROPERTIES"))
                {
                    string slabSection = e2kSections.TryGetValue("SLAB PROPERTIES", out string slabValue) ? slabValue : "";
                    string deckSection = e2kSections.TryGetValue("DECK PROPERTIES", out string deckValue) ? deckValue : "";
                    model.Properties.FloorProperties = _floorPropertiesExporter.Export(slabSection, deckSection);
                }

                // Parse diaphragms
                if (e2kSections.TryGetValue("DIAPHRAGM NAMES", out string diaphragmsSection))
                {
                    model.Properties.Diaphragms = _diaphragmsExporter.Import(diaphragmsSection);
                }

                // Initialize loads container
                model.Loads = new LoadContainer();

                // Parse load definitions
                if (e2kSections.TryGetValue("LOAD PATTERNS", out string loadPatternsSection))
                {
                    model.Loads.LoadDefinitions = _loadDefinitionsExporter.Export(loadPatternsSection);

                    // Set load definitions for other load importers
                    _surfaceLoadsExporter.SetLoadDefinitions(model.Loads.LoadDefinitions);
                    _loadCombinationsExporter.SetLoadDefinitions(model.Loads.LoadDefinitions);
                }

                // Parse load combinations
                if (e2kSections.TryGetValue("LOAD COMBINATIONS", out string loadCombosSection))
                {
                    model.Loads.LoadCombinations = _loadCombinationsExporter.Export(loadCombosSection, model.Loads.LoadDefinitions);
                }

                // Parse surface loads
                if (e2kSections.TryGetValue("SHELL UNIFORM LOAD SETS", out string shellUniformLoadSetsSection))
                {
                    string shellObjectLoadsSection = e2kSections.TryGetValue("SHELL OBJECT LOADS", out string loadsValue) ? loadsValue : "";
                    model.Loads.SurfaceLoads = _surfaceLoadsExporter.Export(shellUniformLoadSetsSection, shellObjectLoadsSection);
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
                _elementsExporter.ParseE2KSections(e2kSections);
                _elementsExporter.SetupReferences(
                    model.ModelLayout.Levels,
                    model.Elements.Openings,
                    model.Properties.FrameProperties,
                    model.Properties.FloorProperties,
                    model.Properties.WallProperties,
                    model.Properties.Diaphragms);

                // Export elements
                model.Elements = _elementsExporter.ExportElements();

                return model;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error importing from E2K: {ex.Message}", ex);
            }
        }
    }
}
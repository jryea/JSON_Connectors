using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using CM = Core.Models.Metadata;
using Revit.Utilities;

namespace Revit.Export
{
    public class ExportManager
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;
        private BaseModel _model;

        public ExportManager(Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
            _model = new BaseModel();
        }

        public int ExportToJson(string filePath)
        {
            // Create default filters (all enabled)
            Dictionary<string, bool> elementFilters = new Dictionary<string, bool>
            {
                { "Grids", true },
                { "Beams", true },
                { "Braces", true },
                { "Columns", true },
                { "Floors", true },
                { "Walls", true },
                { "Footings", true }
            };

            Dictionary<string, bool> materialFilters = new Dictionary<string, bool>
            {
                { "Steel", true },
                { "Concrete", true }
            };

            return ExportToJson(filePath, elementFilters, materialFilters, null, null);
        }

        public int ExportToJson(string filePath, Dictionary<string, bool> elementFilters,
                               Dictionary<string, bool> materialFilters,
                               List<ElementId> selectedLevelIds = null,
                               ElementId baseLevelId = null)
        {
            int totalExported = 0;

            try
            {
                // Initialize metadata
                InitializeMetadata();

                // Export layout elements first
                totalExported += ExportLayoutElements(selectedLevelIds, baseLevelId);

                // Create unique FloorTypes from Levels specifically for Revit
                CreateFloorTypesFromLevels();

                // Export materials first so we have their IDs for referencing
                totalExported += ExportMaterials(materialFilters);

                // Then export other property definitions that reference materials
                totalExported += ExportProperties(materialFilters);

                // Export structural elements
                totalExported += ExportStructuralElements(elementFilters, selectedLevelIds);

                // Save the model to file
                JsonConverter.SaveToFile(_model, filePath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Export Error", $"Error exporting model: {ex.Message}");
            }

            return totalExported;
        }

        // Create unique FloorTypes based on Levels for Revit export only
        private void CreateFloorTypesFromLevels()
        {
            // Clear any existing floor types
            _model.ModelLayout.FloorTypes = new List<Core.Models.ModelLayout.FloorType>();

            // Create a unique FloorType for each Level
            foreach (var level in _model.ModelLayout.Levels)
            {
                // Generate a unique floor type ID
                string floorTypeId = Core.Utilities.IdGenerator.Generate(Core.Utilities.IdGenerator.Layout.FLOOR_TYPE);

                // Create a new FloorType using the level name
                Core.Models.ModelLayout.FloorType floorType = new Core.Models.ModelLayout.FloorType(level.Name);

                // Add to the model's FloorTypes collection
                _model.ModelLayout.FloorTypes.Add(floorType);

                // Associate this FloorType with the Level
                level.FloorTypeId = floorType.Id;
            }

            System.Diagnostics.Debug.WriteLine($"Created {_model.ModelLayout.FloorTypes.Count} unique FloorTypes for Revit export");
        }

        private void InitializeMetadata()
        {
            // Initialize project info
            CM.ProjectInfo projectInfo = new CM.ProjectInfo
            {
                ProjectName = _doc.ProjectInformation?.Name ?? _doc.Title,
                ProjectId = _doc.ProjectInformation?.Number ?? Guid.NewGuid().ToString(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };

            // Initialize units
            CM.Units units = new CM.Units
            {
                Length = "inches",
                Force = "pounds",
                Temperature = "fahrenheit"
            };

            // Extract coordinates
            CM.Coordinates coordinates = Helpers.ExtractCoordinateSystem(_doc);

            // Set the metadata containers
            _model.Metadata.ProjectInfo = projectInfo;
            _model.Metadata.Units = units;
            _model.Metadata.Coordinates = coordinates;
        }

        private int ExportLayoutElements(List<ElementId> selectedLevelIds = null, ElementId baseLevelId = null)
        {
            int count = 0;

            // Export levels
            Export.ModelLayout.LevelExport levelExport = new Export.ModelLayout.LevelExport(_doc);
            count += levelExport.Export(_model.ModelLayout.Levels, selectedLevelIds);

            // If a base level is specified, adjust elevations relative to it
            if (baseLevelId != null)
            {
                AdjustElevationsForBaseLevel(baseLevelId);
            }

            // Export grids
            Export.ModelLayout.GridExport gridExport = new Export.ModelLayout.GridExport(_doc);
            count += gridExport.Export(_model.ModelLayout.Grids);

            return count;
        }

        private void AdjustElevationsForBaseLevel(ElementId baseLevelId)
        {
            // Find the base level in Revit
            Level baseLevel = _doc.GetElement(baseLevelId) as Level;
            if (baseLevel == null) return;

            double baseElevation = baseLevel.Elevation;

            // Find the corresponding level in our model
            var modelBaseLevel = _model.ModelLayout.Levels.Find(l =>
                Math.Abs(l.Elevation - (baseElevation * 12.0)) < 0.1 || // Compare elevation
                l.Name == baseLevel.Name); // Or compare name as fallback

            if (modelBaseLevel == null) return;

            // Use the elevation of the base level as zero reference
            double offset = modelBaseLevel.Elevation;

            // Adjust all level elevations
            foreach (var level in _model.ModelLayout.Levels)
            {
                level.Elevation -= offset;
            }
        }

        private int ExportMaterials(Dictionary<string, bool> materialFilters)
        {
            // Export materials first so we can reference them
            Export.Properties.MaterialExport materialExport = new Export.Properties.MaterialExport(_doc);
            int materialCount = materialExport.Export(_model.Properties.Materials, materialFilters);

            System.Diagnostics.Debug.WriteLine($"Exported {materialCount} materials");
            return materialCount;
        }

        private int ExportProperties(Dictionary<string, bool> materialFilters)
        {
            int count = 0;

            // Export wall properties
            Export.Properties.WallPropertiesExport wallPropertiesExport = new Export.Properties.WallPropertiesExport(_doc);
            count += wallPropertiesExport.Export(_model.Properties.WallProperties);

            // Export floor properties
            Export.Properties.FloorPropertiesExport floorPropertiesExport = new Export.Properties.FloorPropertiesExport(_doc);
            count += floorPropertiesExport.Export(_model.Properties.FloorProperties);

            // Export frame properties - pass the exported materials for correct ID mapping
            Export.Properties.FramePropertiesExport framePropertiesExport = new Export.Properties.FramePropertiesExport(_doc);
            count += framePropertiesExport.Export(_model.Properties.FrameProperties, _model.Properties.Materials);

            return count;
        }

        private int ExportStructuralElements(Dictionary<string, bool> elementFilters, List<ElementId> selectedLevelIds = null)
        {
            int count = 0;

            // Export elements based on filter settings
            if (elementFilters["Walls"])
            {
                Export.Elements.WallExport wallExport = new Export.Elements.WallExport(_doc);
                count += wallExport.Export(_model.Elements.Walls, _model);
            }

            if (elementFilters["Floors"])
            {
                Export.Elements.FloorExport floorExport = new Export.Elements.FloorExport(_doc);
                count += floorExport.Export(_model.Elements.Floors, _model);
            }

            if (elementFilters["Columns"])
            {
                Export.Elements.ColumnExport columnExport = new Export.Elements.ColumnExport(_doc);
                count += columnExport.Export(_model.Elements.Columns, _model);
            }

            if (elementFilters["Beams"])
            {
                Export.Elements.BeamExport beamExport = new Export.Elements.BeamExport(_doc);
                count += beamExport.Export(_model.Elements.Beams, _model);
            }

            if (elementFilters["Braces"])
            {
                Export.Elements.BraceExport braceExport = new Export.Elements.BraceExport(_doc);
                count += braceExport.Export(_model.Elements.Braces, _model);
            }

            if (elementFilters["Footings"])
            {
                // Export spread footings
                Export.Elements.IsolatedFootingExport isolatedFootingExport = new Export.Elements.IsolatedFootingExport(_doc);
                System.Diagnostics.Debug.WriteLine($"Starting isolated footing export, collection initialized: {_model.Elements.IsolatedFootings != null}");
                int footingsExported = isolatedFootingExport.Export(_model.Elements.IsolatedFootings, _model);
                System.Diagnostics.Debug.WriteLine($"Finished isolated footing export: {footingsExported} footings exported");
                count += footingsExported;
            }

            return count;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Properties;
using Core.Models.ModelLayout;
using Core.Models.Metadata;
using Revit.Export.ModelLayout;
using Revit.Export.Elements;
using Revit.Export.Properties;

namespace Revit.Export
{
    public class ExportManager
    {
        private readonly DB.Document _doc;
        private readonly UIApplication _uiApp;
        private BaseModel _model;

        public ExportManager(DB.Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
            _model = new BaseModel();
        }

        public int ExportToJson(string filePath)
        {
            int totalExported = 0;

            try
            {
                // Initialize metadata
                InitializeMetadata();

                // Export layout elements first
                totalExported += ExportLayoutElements();

                // Create unique FloorTypes from Levels specifically for Revit
                CreateFloorTypesFromLevels();

                // Export materials first so we have their IDs for referencing
                totalExported += ExportMaterials();

                // Then export other property definitions that reference materials
                totalExported += ExportProperties();

                // Export structural elements
                totalExported += ExportStructuralElements();

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
            _model.ModelLayout.FloorTypes = new List<FloorType>();

            // Create a unique FloorType for each Level
            foreach (var level in _model.ModelLayout.Levels)
            {
                // Generate a unique floor type ID
                string floorTypeId = Core.Utilities.IdGenerator.Generate(Core.Utilities.IdGenerator.Layout.FLOOR_TYPE);

                // Create a new FloorType using the level name
                FloorType floorType = new FloorType
                {
                    Id = floorTypeId,
                    Name = level.Name, // Use level name as the floor type name
                    Description = $"Floor type for {level.Name}"
                };

                // Add to the model's FloorTypes collection
                _model.ModelLayout.FloorTypes.Add(floorType);

                // Associate this FloorType with the Level
                level.FloorTypeId = floorTypeId;
            }

            Debug.WriteLine($"Created {_model.ModelLayout.FloorTypes.Count} unique FloorTypes for Revit export");
        }

        private void InitializeMetadata()
        {
            // Initialize project info
            ProjectInfo projectInfo = new ProjectInfo
            {
                ProjectName = _doc.ProjectInformation?.Name ?? _doc.Title,
                ProjectId = _doc.ProjectInformation?.Number ?? Guid.NewGuid().ToString(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };

            // Initialize units
            Units units = new Units
            {
                Length = "inches",
                Force = "pounds",
                Temperature = "fahrenheit"
            };

            _model.Metadata.ProjectInfo = projectInfo;
            _model.Metadata.Units = units;
        }

        private int ExportLayoutElements()
        {
            int count = 0;

            // Export levels
            LevelExport levelExport = new LevelExport(_doc);
            count += levelExport.Export(_model.ModelLayout.Levels);

            // Export grids
            GridExport gridExport = new GridExport(_doc);
            count += gridExport.Export(_model.ModelLayout.Grids);

            return count;
        }

        private int ExportMaterials()
        {
            // Export materials first so we can reference them
            MaterialExport materialExport = new MaterialExport(_doc);
            int materialCount = materialExport.Export(_model.Properties.Materials);

            Debug.WriteLine($"Exported {materialCount} materials");
            return materialCount;
        }

        private int ExportProperties()
        {
            int count = 0;

            // Export wall properties
            WallPropertiesExport wallPropertiesExport = new WallPropertiesExport(_doc);
            count += wallPropertiesExport.Export(_model.Properties.WallProperties);

            // Export floor properties
            FloorPropertiesExport floorPropertiesExport = new FloorPropertiesExport(_doc);
            count += floorPropertiesExport.Export(_model.Properties.FloorProperties);

            // Export frame properties - pass the exported materials for correct ID mapping
            FramePropertiesExport framePropertiesExport = new FramePropertiesExport(_doc);
            count += framePropertiesExport.Export(_model.Properties.FrameProperties, _model.Properties.Materials);

            return count;
        }

        private int ExportStructuralElements()
        {
            int count = 0;

            // Export walls
            WallExport wallExport = new WallExport(_doc);
            count += wallExport.Export(_model.Elements.Walls, _model);

            // Export floors
            FloorExport floorExport = new FloorExport(_doc);
            count += floorExport.Export(_model.Elements.Floors, _model);

            // Export columns
            ColumnExport columnExport = new ColumnExport(_doc);
            count += columnExport.Export(_model.Elements.Columns, _model);

            // Export beams
            BeamExport beamExport = new BeamExport(_doc);
            count += beamExport.Export(_model.Elements.Beams, _model);

            // Export braces
            BraceExport braceExport = new BraceExport(_doc);
            count += braceExport.Export(_model.Elements.Braces, _model);

            // Export spread footings
            IsolatedFootingExport isolatedFootingExport = new IsolatedFootingExport(_doc);
            Debug.WriteLine($"Starting isolated footing export, collection initialized: {_model.Elements.IsolatedFootings != null}");
            int footingsExported = isolatedFootingExport.Export(_model.Elements.IsolatedFootings, _model);
            Debug.WriteLine($"Finished isolated footing export: {footingsExported} footings exported");
            count += footingsExported;

            return count;
        }
    }
}
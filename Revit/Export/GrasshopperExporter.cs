using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models.Geometry;
using CE = Core.Models.Elements;
using CM = Core.Models.Metadata;    
using CL = Core.Models.ModelLayout;
using Core.Models;
using Core.Utilities;
using Revit.Export.Elements;
using Revit.Export.ModelLayout;
using Revit.Export.Properties;
using Revit.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit.Export
{
    public class GrasshopperExporter
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;
        private BaseModel _model;

        public GrasshopperExporter(Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
            _model = new BaseModel();
        }

        public void ExportAll(string jsonPath, string dwgFolder)
        {
            // Initialize metadata including project coordinates
            InitializeMetadata();

            // Export elements reusing your existing export functionality
            ExportModelStructure();

            // Export CAD plans
            CADExporter cadExporter = new CADExporter(_doc);
            cadExporter.ExportCADPlans(dwgFolder);

            // Save the model to JSON
            JsonConverter.SaveToFile(_model, jsonPath);
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

            // Set project info and units
            _model.Metadata.ProjectInfo = projectInfo;
            _model.Metadata.Units = units;

            // Add coordinate system info using the helper method
            _model.Metadata.Coordinates = Helpers.ExtractCoordinateSystem(_doc);
        }

        private void ExportModelStructure()
        {
            // Export layout elements first
            ExportLayoutElements();

            // Create unique FloorTypes from Levels
            CreateFloorTypesFromLevels();

            // Export structural elements
            ExportStructuralElements();
        }

        private void ExportLayoutElements()
        {
            int count = 0;

            // Export levels
            LevelExport levelExport = new LevelExport(_doc);
            count += levelExport.Export(_model.ModelLayout.Levels);
            Debug.WriteLine($"Exported {_model.ModelLayout.Levels.Count} levels");

            // Export grids
            GridExport gridExport = new GridExport(_doc);
            count += gridExport.Export(_model.ModelLayout.Grids);
            Debug.WriteLine($"Exported {_model.ModelLayout.Grids.Count} grids");
        }

        private void CreateFloorTypesFromLevels()
        {
            // Clear any existing floor types
            _model.ModelLayout.FloorTypes = new List<CL.FloorType>();

            // Create a unique FloorType for each Level
            foreach (var level in _model.ModelLayout.Levels)
            {
                // Generate a unique floor type ID
                string floorTypeId = Core.Utilities.IdGenerator.Generate(Core.Utilities.IdGenerator.Layout.FLOOR_TYPE);

                // Create a new FloorType using the level name
                CL.FloorType floorType = new CL.FloorType
                {
                    Id = floorTypeId,
                    Name = level.Name,
                    Description = $"Floor type for {level.Name}"
                };

                // Add to the model's FloorTypes collection
                _model.ModelLayout.FloorTypes.Add(floorType);

                // Associate this FloorType with the Level
                level.FloorTypeId = floorTypeId;
            }

            Debug.WriteLine($"Created {_model.ModelLayout.FloorTypes.Count} unique FloorTypes for Grasshopper export");
        }

        private void ExportStructuralElements()
        {
            int count = 0;

            // Export floors
            FloorExport floorExport = new FloorExport(_doc);
            count += floorExport.Export(_model.Elements.Floors, _model);
            Debug.WriteLine($"Exported {_model.Elements.Floors.Count} floors");

            // Export columns
            ColumnExport columnExport = new ColumnExport(_doc);
            count += columnExport.Export(_model.Elements.Columns, _model);
            Debug.WriteLine($"Exported {_model.Elements.Columns.Count} columns");
        }
    }
}
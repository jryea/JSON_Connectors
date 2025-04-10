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
using Revit.Export;
using Revit.Export.ModelLayout;
using Revit.Export.Elements;
using Revit.Export.Properties;
using Revit.Import.Elements;
using Revit.Import.ModelLayout;

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

                // Export property definitions
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
                Length = _doc.GetUnits().GetFormatOptions(DB.SpecTypeId.Length).GetSymbolTypeId().ToString(),
                Force = _doc.GetUnits().GetFormatOptions(DB.SpecTypeId.Force).GetSymbolTypeId().ToString(),
                Temperature = _doc.GetUnits().GetFormatOptions(DB.SpecTypeId.HvacTemperature).GetSymbolTypeId().ToString()
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

        private int ExportProperties()
        {
            int count = 0;

            // Export materials
            MaterialExport materialExport = new MaterialExport(_doc);
            count += materialExport.Export(_model.Properties.Materials);

            // Export wall properties
            WallPropertiesExport wallPropertiesExport = new WallPropertiesExport(_doc);
            count += wallPropertiesExport.Export(_model.Properties.WallProperties);

            // Export floor properties
            FloorPropertiesExport floorPropertiesExport = new FloorPropertiesExport(_doc);
            count += floorPropertiesExport.Export(_model.Properties.FloorProperties);

            // Export frame properties
            FramePropertiesExport framePropertiesExport = new FramePropertiesExport(_doc);
            count += framePropertiesExport.Export(_model.Properties.FrameProperties);

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

            return count;
        }
    }
}
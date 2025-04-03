// RAMExporter.cs - Main entry point for export operations
using System;
using Core.Models;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class RAMExporter
    {
        private RamDataAccess1 _ramDataAccess;
        private IDBIO1 _database;
        private IModel _model;

        public RAMExporter()
        {
            _ramDataAccess = new RamDataAccess1();
        }

        public bool ExportModel(BaseModel model, string filePath)
        {
            try
            {
                // Initialize database
                _database = _ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
                _database.CreateNewDatabase2(filePath, EUnits.eUnitsEnglish, "1");
                _model = _ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;

                // Export model components
                ExportFloorTypes(model);
                ExportGrids(model);
                ExportStories(model);
                ExportMaterials(model);
                ExportProperties(model);
                ExportElements(model);
                ExportLoads(model);

                // Save and close database
                _database.SaveDatabase();
                _database.CloseDatabase();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting to RAM: {ex.Message}");
                if (_database != null)
                {
                    try
                    {
                        _database.CloseDatabase();
                    }
                    catch { }
                }
                return false;
            }
        }

        private void ExportFloorTypes(BaseModel model)
        {
            var exporter = new FloorTypeExporter(_model);
            exporter.Export(model);
        }

        private void ExportGrids(BaseModel model)
        {
            var exporter = new GridExporter(_model);
            exporter.Export(model);
        }

        private void ExportStories(BaseModel model)
        {
            var exporter = new StoryExporter(_model);
            exporter.Export(model);
        }

        private void ExportMaterials(BaseModel model)
        {
            var exporter = new MaterialExporter(_model);
            exporter.Export(model);
        }

        private void ExportProperties(BaseModel model)
        {
            // Export various property types
            var slabExporter = new SlabPropertiesExporter(_model);
            slabExporter.Export(model);

            var compDeckExporter = new CompositeDeckPropertiesExporter(_model);
            compDeckExporter.Export(model);

            var nonCompDeckExporter = new NonCompositeDeckPropertiesExporter(_model);
            nonCompDeckExporter.Export(model);

            var surfaceLoadExporter = new SurfaceLoadPropertiesExporter(_model);
            surfaceLoadExporter.Export(model);
        }

        private void ExportElements(BaseModel model)
        {
            var beamExporter = new BeamExporter(_model);
            beamExporter.Export(model);

            var columnExporter = new ColumnExporter(_model);
            columnExporter.Export(model);

            var wallExporter = new WallExporter(_model);
            wallExporter.Export(model);
        }

        private void ExportLoads(BaseModel model)
        {
            // Implement load exporting
        }
    }
}
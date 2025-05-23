using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using Revit.ViewModels;

namespace Revit.Import
{
    public class ImportManager
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;

        public ImportManager(Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
        }

        public int ImportFromFile(string filePath, Dictionary<string, bool> elementFilters,
            Dictionary<string, bool> materialFilters, ImportTransformationParameters transformParams)
        {
            try
            {
                // Create import context
                var context = new ImportContext(_doc, _uiApp)
                {
                    FilePath = filePath,
                    ElementFilters = elementFilters ?? new Dictionary<string, bool>(),
                    MaterialFilters = materialFilters ?? new Dictionary<string, bool>(),
                    TransformationParams = transformParams
                };

                // Use clean architecture
                var importer = new StructuralModelImporter();
                var model = importer.Import(context);

                // Import to Revit
                int importedCount = ImportToRevit(model, context);

                return importedCount;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Import Error", $"Error importing model: {ex.Message}");
                return 0;
            }
        }

        private int ImportToRevit(BaseModel model, ImportContext context)
        {
            int totalImported = 0;

            using (Transaction transaction = new Transaction(_doc, "Import Structural Model"))
            {
                transaction.Start();

                try
                {
                    // Import grids first
                    if (context.ShouldImportElement("Grids") && model.ModelLayout?.Grids != null)
                    {
                        var gridImport = new ModelLayout.GridImport(_doc);
                        totalImported += gridImport.Import(model.ModelLayout.Grids);
                    }

                    // Import levels
                    if (model.ModelLayout?.Levels != null)
                    {
                        var levelImport = new ModelLayout.LevelImport(_doc);
                        totalImported += levelImport.Import(model.ModelLayout.Levels);
                    }

                    // Import other elements using existing import classes
                    if (context.ShouldImportElement("Beams") && model.Elements?.Beams != null)
                    {
                        var beamImport = new Elements.BeamImport(_doc);
                        totalImported += beamImport.Import(model.Elements.Beams, model);
                    }

                    if (context.ShouldImportElement("Columns") && model.Elements?.Columns != null)
                    {
                        var columnImport = new Elements.ColumnImport(_doc);
                        totalImported += columnImport.Import(model.Elements.Columns, model);
                    }

                    if (context.ShouldImportElement("Braces") && model.Elements?.Braces != null)
                    {
                        var braceImport = new Elements.BraceImport(_doc);
                        totalImported += braceImport.Import(model.Elements.Braces, model);
                    }

                    if (context.ShouldImportElement("Walls") && model.Elements?.Walls != null)
                    {
                        var wallImport = new Elements.WallImport(_doc);
                        totalImported += wallImport.Import(model.Elements.Walls, model);
                    }

                    if (context.ShouldImportElement("Floors") && model.Elements?.Floors != null)
                    {
                        var floorImport = new Elements.FloorImport(_doc);
                        totalImported += floorImport.Import(model.Elements.Floors, model);
                    }

                    if (context.ShouldImportElement("Footings") && model.Elements?.IsolatedFootings != null)
                    {
                        var footingImport = new Elements.IsolatedFootingImport(_doc);
                        totalImported += footingImport.Import(model.Elements.IsolatedFootings, model);
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.RollBack();
                    throw;
                }
            }

            return totalImported;
        }
    }
}
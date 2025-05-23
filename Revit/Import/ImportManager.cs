using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using Revit.ViewModels;
using System.Diagnostics;

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

        public int ImportFromJson(string filePath)
        {
            // Create default filters (import everything)
            var elementFilters = new Dictionary<string, bool>
            {
                { "Grids", true },
                { "Beams", true },
                { "Braces", true },
                { "Columns", true },
                { "Floors", true },
                { "Walls", true },
                { "Footings", true }
            };

            var materialFilters = new Dictionary<string, bool>
            {
                { "Steel", true },
                { "Concrete", true }
            };

            // No transformations for basic import
            var transformParams = new ImportTransformationParameters();

            return ImportFromFile(filePath, elementFilters, materialFilters, transformParams);
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
                    // Create level mapping first
                    Dictionary<string, ElementId> levelIdMap = CreateLevelMapping(model);

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
                        totalImported += levelImport.Import(model.ModelLayout.Levels, levelIdMap);
                    }

                    // Import beams
                    if (context.ShouldImportElement("Beams") && model.Elements?.Beams != null)
                    {
                        var beamImport = new Elements.BeamImport(_doc);
                        totalImported += beamImport.Import(model.Elements.Beams, levelIdMap, model);
                    }

                    // Import columns
                    if (context.ShouldImportElement("Columns") && model.Elements?.Columns != null)
                    {
                        var columnImport = new Elements.ColumnImport(_doc);
                        totalImported += columnImport.Import(model.Elements.Columns, levelIdMap, model);
                    }

                    // Import braces
                    if (context.ShouldImportElement("Braces") && model.Elements?.Braces != null)
                    {
                        var braceImport = new Elements.BraceImport(_doc);
                        totalImported += braceImport.Import(model.Elements.Braces, levelIdMap, model);
                    }

                    // Import walls
                    if (context.ShouldImportElement("Walls") && model.Elements?.Walls != null)
                    {
                        var wallImport = new Elements.WallImport(_doc);
                        totalImported += wallImport.Import(model.Elements.Walls, levelIdMap, model);
                    }

                    // Import floors
                    if (context.ShouldImportElement("Floors") && model.Elements?.Floors != null)
                    {
                        var floorImport = new Elements.FloorImport(_doc);
                        totalImported += floorImport.Import(levelIdMap, model);
                    }

                    // Import footings
                    if (context.ShouldImportElement("Footings") && model.Elements?.IsolatedFootings != null)
                    {
                        var footingImport = new Elements.IsolatedFootingImport(_doc);
                        totalImported += footingImport.Import(model.Elements.IsolatedFootings, levelIdMap, model);
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

        private Dictionary<string, ElementId> CreateLevelMapping(BaseModel model)
        {
            var mapping = new Dictionary<string, ElementId>();

            if (model.ModelLayout?.Levels == null) return mapping;

            var revitLevels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            foreach (var modelLevel in model.ModelLayout.Levels)
            {
                var revitLevel = revitLevels.FirstOrDefault(l =>
                    l.Name == modelLevel.Name ||
                    Math.Abs(l.Elevation - (modelLevel.Elevation / 12.0)) < 0.1);

                if (revitLevel != null)
                {
                    mapping[modelLevel.Id] = revitLevel.Id;
                }
            }

            return mapping;
        }
    }
}
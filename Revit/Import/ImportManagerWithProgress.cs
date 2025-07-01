using System;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Views;
using Revit.ViewModels;

namespace Revit.Import
{
    public class ImportManagerWithProgress
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;
        private ImportProgressWindow _progressWindow;
        private ImportProgressViewModel _progressViewModel;

        public ImportManagerWithProgress(Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
        }

        public async Task<int> ImportFromFileWithProgressAsync(
            string filePath,
            Dictionary<string, bool> elementFilters,
            Dictionary<string, bool> materialFilters,
            ImportTransformationParameters transformParams)
        {
            // Create and show progress window
            _progressWindow = new ImportProgressWindow();
            _progressViewModel = _progressWindow.ViewModel;

            // Show window modally on UI thread
            _progressWindow.Show();

            try
            {
                // Start the import process
                return await Task.Run(() => PerformImportWithProgress(
                    filePath, elementFilters, materialFilters, transformParams));
            }
            catch (OperationCanceledException)
            {
                _progressViewModel.AppendLog("Import cancelled by user");
                return 0;
            }
            catch (Exception ex)
            {
                _progressViewModel.AppendLog($"Import failed: {ex.Message}");
                MessageBox.Show($"Import failed: {ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return 0;
            }
            finally
            {
                // Close progress window after a short delay to show completion
                await Task.Delay(2000);
                _progressWindow?.SafeClose();
            }
        }

        private int PerformImportWithProgress(
            string filePath,
            Dictionary<string, bool> elementFilters,
            Dictionary<string, bool> materialFilters,
            ImportTransformationParameters transformParams)
        {
            _progressViewModel.AppendLog($"Starting import from: {filePath}");

            int totalImported = 0;

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

                // Step 1: File Conversion & Loading (using existing ImportManager logic)
                _progressViewModel.StartConversion();
                _progressViewModel.AppendLog("Starting model import process");

                // Use the existing StructuralModelImporter to handle the entire process
                var importer = new StructuralModelImporter();
                var model = importer.Import(context);

                _progressViewModel.CompleteConversion();
                _progressViewModel.StartLoading();
                _progressViewModel.CompleteLoading();
                _progressViewModel.AppendLog($"Model loaded with elements");

                // Check for cancellation
                _progressViewModel.CancellationToken.ThrowIfCancellationRequested();

                // Step 2: Import Elements using existing ImportManager
                var importManager = new ImportManager(_doc, _uiApp);
                totalImported = ImportElementsWithProgress(model, context, importManager);

                _progressViewModel.CompleteImport(totalImported);
                _progressViewModel.AppendLog($"Import completed successfully - {totalImported} elements imported");

                return totalImported;
            }
            catch (OperationCanceledException)
            {
                _progressViewModel.AppendLog("Import cancelled by user");
                throw;
            }
            catch (Exception ex)
            {
                _progressViewModel.AppendLog($"Import error: {ex.Message}");
                throw;
            }
        }

        private int ImportElementsWithProgress(
            Core.Models.BaseModel model,
            ImportContext context,
            ImportManager importManager)
        {
            int totalImported = 0;

            // Import each element type with progress reporting
            totalImported += ImportElementTypeWithProgress("Grids", () =>
                ImportGridsFromModel(model, context));

            totalImported += ImportElementTypeWithProgress("Beams", () =>
                ImportBeamsFromModel(model, context));

            totalImported += ImportElementTypeWithProgress("Columns", () =>
                ImportColumnsFromModel(model, context));

            totalImported += ImportElementTypeWithProgress("Braces", () =>
                ImportBracesFromModel(model, context));

            totalImported += ImportElementTypeWithProgress("Walls", () =>
                ImportWallsFromModel(model, context));

            totalImported += ImportElementTypeWithProgress("Floors", () =>
                ImportFloorsFromModel(model, context));

            return totalImported;
        }

        private int ImportElementTypeWithProgress(string elementType, Func<int> importFunction)
        {
            try
            {
                // Check if this element type should be imported
                if (!ShouldImportElementType(elementType))
                {
                    _progressViewModel.SkipElementImport(elementType, "Disabled in filters");
                    return 0;
                }

                // Check for cancellation before starting
                _progressViewModel.CancellationToken.ThrowIfCancellationRequested();

                _progressViewModel.StartElementImport(elementType);
                _progressViewModel.AppendLog($"Starting {elementType.ToLower()} import");

                int count = importFunction();

                _progressViewModel.CompleteElementImport(elementType, count);
                _progressViewModel.AppendLog($"Completed {elementType.ToLower()} import: {count} elements");

                return count;
            }
            catch (OperationCanceledException)
            {
                _progressViewModel.AppendLog($"{elementType} import cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _progressViewModel.FailElementImport(elementType, ex.Message);
                _progressViewModel.AppendLog($"{elementType} import failed: {ex.Message}");
                return 0; // Continue with other elements
            }
        }

        // Element import methods using the existing import infrastructure
        private int ImportGridsFromModel(Core.Models.BaseModel model, ImportContext context)
        {
            if (model.ModelLayout?.Grids == null || !context.ShouldImportElement("Grids"))
                return 0;

            try
            {
                var gridImport = new ModelLayout.GridImport(_doc);
                return gridImport.Import(model.ModelLayout.Grids);
            }
            catch (Exception ex)
            {
                _progressViewModel.AppendLog($"Grid import error: {ex.Message}");
                return 0;
            }
        }

        private int ImportBeamsFromModel(Core.Models.BaseModel model, ImportContext context)
        {
            if (model.Elements?.Beams == null || !context.ShouldImportElement("Beams"))
                return 0;

            try
            {
                // Create level mapping from existing ImportManager logic
                var levelIdMap = CreateLevelIdMapping(model);
                var beamImport = new Elements.BeamImport(_doc);
                return beamImport.Import(model.Elements.Beams, levelIdMap, model);
            }
            catch (Exception ex)
            {
                _progressViewModel.AppendLog($"Beam import error: {ex.Message}");
                return 0;
            }
        }

        private int ImportColumnsFromModel(Core.Models.BaseModel model, ImportContext context)
        {
            if (model.Elements?.Columns == null || !context.ShouldImportElement("Columns"))
                return 0;

            try
            {
                var levelIdMap = CreateLevelIdMapping(model);
                var columnImport = new Elements.ColumnImport(_doc);
                return columnImport.Import(model.Elements.Columns, levelIdMap, model);
            }
            catch (Exception ex)
            {
                _progressViewModel.AppendLog($"Column import error: {ex.Message}");
                return 0;
            }
        }

        private int ImportBracesFromModel(Core.Models.BaseModel model, ImportContext context)
        {
            if (model.Elements?.Braces == null || !context.ShouldImportElement("Braces"))
                return 0;

            try
            {
                var levelIdMap = CreateLevelIdMapping(model);
                var braceImport = new Elements.BraceImport(_doc);
                return braceImport.Import(model.Elements.Braces, levelIdMap, model);
            }
            catch (Exception ex)
            {
                _progressViewModel.AppendLog($"Brace import error: {ex.Message}");
                return 0;
            }
        }

        private int ImportWallsFromModel(Core.Models.BaseModel model, ImportContext context)
        {
            if (model.Elements?.Walls == null || !context.ShouldImportElement("Walls"))
                return 0;

            try
            {
                var levelIdMap = CreateLevelIdMapping(model);
                var wallImport = new Elements.WallImport(_doc);
                return wallImport.Import(model.Elements.Walls, levelIdMap, model);
            }
            catch (Exception ex)
            {
                _progressViewModel.AppendLog($"Wall import error: {ex.Message}");
                return 0;
            }
        }

        private int ImportFloorsFromModel(Core.Models.BaseModel model, ImportContext context)
        {
            if (model.Elements?.Floors == null || !context.ShouldImportElement("Floors"))
                return 0;

            try
            {
                var levelIdMap = CreateLevelIdMapping(model);
                var floorImport = new Elements.FloorImport(_doc);
                // FloorImport.Import takes (levelIdMap, model) - only 2 parameters
                return floorImport.Import(levelIdMap, model);
            }
            catch (Exception ex)
            {
                _progressViewModel.AppendLog($"Floor import error: {ex.Message}");
                return 0;
            }
        }

        private Dictionary<string, ElementId> CreateLevelIdMapping(Core.Models.BaseModel model)
        {
            // Create level mapping - this logic should match your existing ImportManager
            var levelIdMap = new Dictionary<string, ElementId>();

            try
            {
                // First try to use the existing ImportManager's CreateLevelMapping method
                // which maps model levels to existing Revit levels
                if (model.ModelLayout?.Levels != null)
                {
                    // Use the same logic as ImportManager.CreateLevelMapping
                    var revitLevels = new FilteredElementCollector(_doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .ToList();

                    foreach (var modelLevel in model.ModelLayout.Levels)
                    {
                        // Try to find existing level by name first
                        var revitLevel = revitLevels.FirstOrDefault(l => l.Name == modelLevel.Name);

                        // If not found by name, try by elevation
                        if (revitLevel == null)
                        {
                            revitLevel = revitLevels.FirstOrDefault(l =>
                                Math.Abs(l.Elevation - (modelLevel.Elevation / 12.0)) < 0.1);
                        }

                        if (revitLevel != null)
                        {
                            levelIdMap[modelLevel.Id] = revitLevel.Id;
                            _progressViewModel.AppendLog($"Mapped level {modelLevel.Name} to existing {revitLevel.Name}");
                        }
                        else
                        {
                            _progressViewModel.AppendLog($"No existing level found for {modelLevel.Name}");
                        }
                    }
                }

                // If no custom levels or mapping failed, map to existing Revit levels by name
                if (levelIdMap.Count == 0)
                {
                    // Get existing levels from the document
                    var existingLevels = new FilteredElementCollector(_doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .ToList();

                    // Create a simple mapping by name
                    foreach (var level in existingLevels)
                    {
                        levelIdMap[level.Name] = level.Id;
                    }

                    _progressViewModel.AppendLog($"Created fallback mapping with {levelIdMap.Count} existing levels");
                }
            }
            catch (Exception ex)
            {
                _progressViewModel.AppendLog($"Level mapping error: {ex.Message}");
            }

            return levelIdMap;
        }

        private bool ShouldImportElementType(string elementType)
        {
            // This should use your actual filter logic
            // For now, assume all types should be imported
            return true;
        }
    }
}
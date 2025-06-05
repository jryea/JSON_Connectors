using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Views;
using Revit.Utilities;
using Revit.Import;

namespace Revit.Import
{
    [Transaction(TransactionMode.Manual)]
    public class ImportStructuralModelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Show the custom import dialog to get parameters
                ImportStructuralModelWindow importWindow = new ImportStructuralModelWindow(uiApp);
                bool? dialogResult = importWindow.ShowDialog();

                if (!dialogResult.HasValue || !dialogResult.Value)
                {
                    return Result.Cancelled;
                }

                // Get import parameters from the dialog
                var viewModel = importWindow.DataContext as Revit.ViewModels.ImportStructuralModelViewModel;
                if (viewModel == null || string.IsNullOrEmpty(viewModel.InputLocation))
                {
                    message = "No input file selected";
                    return Result.Failed;
                }

                // Create import filters from dialog settings
                var elementFilters = new Dictionary<string, bool>
                {
                    { "Grids", viewModel.ImportGrids },
                    { "Beams", viewModel.ImportBeams },
                    { "Braces", viewModel.ImportBraces },
                    { "Columns", viewModel.ImportColumns },
                    { "Floors", viewModel.ImportFloors },
                    { "Walls", viewModel.ImportWalls },
                    { "Footings", viewModel.ImportFootings }
                };

                var materialFilters = new Dictionary<string, bool>
                {
                    { "Steel", viewModel.ImportSteel },
                    { "Concrete", viewModel.ImportConcrete }
                };

                // Create transformation parameters
                var transformParams = new Revit.ViewModels.ImportTransformationParameters
                {
                    UseGridIntersection = viewModel.UseGridIntersection,
                    UseManualRotation = viewModel.UseManualRotation,
                    RotationAngle = viewModel.RotationAngle,
                    BaseLevelElevation = viewModel.BaseLevelElevation
                };

                // Handle file conversion based on type
                string jsonPath = ConvertToJson(viewModel.InputLocation);

                // Perform import using existing ImportManager
                var importManager = new ImportManager(doc, uiApp);
                int importedCount = importManager.ImportFromFile(
                    jsonPath,
                    elementFilters,
                    materialFilters,
                    transformParams);

                TaskDialog.Show("Import Complete",
                    $"Successfully imported {importedCount} elements.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error",
                    $"An error occurred while importing structural model: {ex.Message}");
                return Result.Failed;
            }
        }

        private string ConvertToJson(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            switch (extension)
            {
                case ".json":
                    return filePath;

                case ".rss":
                    return ConvertRAMToJson(filePath);

                case ".e2k":
                    return ConvertETABSToJson(filePath);

                default:
                    throw new NotSupportedException($"File format {extension} is not supported");
            }
        }

        private string ConvertRAMToJson(string ramFilePath)
        {
            string tempJsonPath = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(ramFilePath) + "_temp.json");

            // Use process isolation to avoid SQLite conflicts
            ProcessIsolatedRAM ramConverter = new ProcessIsolatedRAM();
            var conversionResult = ramConverter.ConvertRAMToJSON(ramFilePath, tempJsonPath);

            if (!conversionResult.Success)
            {
                throw new Exception($"Failed to convert RAM file: {conversionResult.Message}");
            }

            return tempJsonPath;
        }

        private string ConvertETABSToJson(string etabsFilePath)
        {
            string tempJsonPath = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(etabsFilePath) + "_temp.json");

            try
            {
                string e2kContent = File.ReadAllText(etabsFilePath);
                var converter = new ETABS.ETABSToGrasshopper();
                string jsonContent = converter.ProcessE2K(e2kContent);
                File.WriteAllText(tempJsonPath, jsonContent);
                return tempJsonPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert ETABS file: {ex.Message}", ex);
            }
        }

        internal static System.Drawing.Bitmap ByteArrayToBitmap(byte[] byteArray)
        {
            using (var ms = new System.IO.MemoryStream(byteArray))
            {
                return new System.Drawing.Bitmap(ms);
            }
        }

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "btnImportStructuralModel";
            string buttonTitle = "Import Model";

            ButtonDataClass myButtonData = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_32),
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_16),
                "Import a structural model from multiple formats");

            return myButtonData.Data;
        }
    }
}
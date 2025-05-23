using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Views;
using Revit.Utilities;
using Revit.Import;
using System.Collections.Generic;

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

                System.Diagnostics.Debug.WriteLine($"Dialog result: {dialogResult}");
                System.Diagnostics.Debug.WriteLine($"DataContext type: {importWindow.DataContext?.GetType()}");

                // If dialog was canceled, return cancelled
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
                    UseImportedGrids = viewModel.UseImportedGrids,
                    Grid1Name = viewModel.Grid1Name,
                    Grid2Name = viewModel.Grid2Name,
                    ImportedGrid1Name = viewModel.ImportedGrid1Name,
                    ImportedGrid2Name = viewModel.ImportedGrid2Name,
                    RotationAngle = viewModel.RotationAngle,
                    BaseLevelElevation = viewModel.BaseLevelElevation
                };

                // Perform import within the command context (like ETABSImportCommand)
                var importManager = new ImportManager(doc, uiApp);
                int importedCount = importManager.ImportFromFile(
                    viewModel.InputLocation,
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
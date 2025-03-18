using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Core.Converters;
using Core.Models;
using CM = Core.Models.Model;
using Core.Models.Elements;
using System.Xml.Linq;

namespace Revit.Import
{
    /// <summary>
    /// External command for importing grids from JSON to Revit
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class GridImporter : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Show file dialog to select JSON file
                string jsonFilePath = ShowFileDialog();
                if (string.IsNullOrEmpty(jsonFilePath))
                {
                    return Result.Cancelled;
                }

                // Load JSON file
                var model = JsonConverter.LoadFromFile(jsonFilePath);

                // Import grids
                using (Transaction transaction = new Transaction(doc, "Import Grids from JSON"))
                {
                    transaction.Start();

                    bool success = ImportGrids(doc, model.Grids);

                    if (success)
                    {
                        transaction.Commit();
                        TaskDialog.Show("Import Successful", $"Successfully imported {model.Grids.Count} grids from JSON file.");
                        return Result.Succeeded;
                    }
                    else
                    {
                        transaction.RollBack();
                        message = "Failed to import grids. See log for details.";
                        return Result.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Shows file dialog to select JSON file using Revit's FileOpenDialog
        /// </summary>
        private string ShowFileDialog()
        {
            // Create Revit's FileOpenDialog
            FileOpenDialog fileOpenDialog = new FileOpenDialog("JSON Files (*.json)|*.json");
            fileOpenDialog.Title = "Select JSON File";
            //fileOpenDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Set default extension
            //fileOpenDialog.DefaultExt = "json";

            // Show dialog and check result
            if (fileOpenDialog.Show() == ItemSelectionDialogResult.Canceled)
            {
                return null;
            }

            // Get selected file
            ModelPath modelPath = fileOpenDialog.GetSelectedModelPath();
            return ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
        }

        /// <summary>
        /// Imports grids from JSON model to Revit
        /// </summary>
        private bool ImportGrids(Document doc, List<CM.Grid> jsonGrids)
        {
            if (jsonGrids == null || jsonGrids.Count == 0)
            {
                TaskDialog.Show("Warning", "No grids found in the JSON file.");
                return false;
            }

            int importedCount = 0;

            foreach (var jsonGrid in jsonGrids)
            {
                try
                {
                    // Convert JSON grid points to Revit XYZ
                    XYZ startPoint = ConvertToRevitCoordinates(jsonGrid.StartPoint);
                    XYZ endPoint = ConvertToRevitCoordinates(jsonGrid.EndPoint);

                    // Create line for grid
                    Line gridLine = Line.CreateBound(startPoint, endPoint);

                    // Create grid
                    Autodesk.Revit.DB.Grid revitGrid = Autodesk.Revit.DB.Grid.Create(doc, gridLine);

                    // Set grid name
                    revitGrid.Name = jsonGrid.Name;

                    // Apply bubble visibility if specified in JSON
                    if (jsonGrid.StartPoint.IsBubble)
                    {
                        revitGrid.ShowBubbleInView(DatumEnds.End0, doc.ActiveView);
                    }
                    else
                    {
                        revitGrid.HideBubbleInView(DatumEnds.End0, doc.ActiveView);
                    }

                    if (jsonGrid.EndPoint.IsBubble)
                    {
                        revitGrid.ShowBubbleInView(DatumEnds.End1, doc.ActiveView);
                    }
                    else
                    {
                        revitGrid.HideBubbleInView(DatumEnds.End1, doc.ActiveView);
                    }

                    importedCount++;
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other grids
                    TaskDialog.Show("Error", $"Error importing grid {jsonGrid.Name}: {ex.Message}");
                }
            }

            return importedCount > 0;
        }

        /// <summary>
        /// Converts JSON GridPoint to Revit XYZ (with unit conversion)
        /// </summary>
        private XYZ ConvertToRevitCoordinates(GridPoint point)
        {
            // Assuming JSON coordinates are in inches, convert to feet for Revit
            double x = point.X / 12.0;
            double y = point.Y / 12.0;
            double z = point.Z / 12.0;

            return new XYZ(x, y, z);
        }
    }
}
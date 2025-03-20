using System;
using System.Diagnostics;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models.Elements;
using Revit.Utils;

namespace Revit.Import
{
    /// <summary>
    /// External command for importing grids from JSON
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class GridImportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // Show file dialog
                FileOpenDialog fileDialog = new FileOpenDialog("JSON Files (*.json)|*.json");
                fileDialog.Title = "Select JSON File";

                if (fileDialog.Show() != ItemSelectionDialogResult.Canceled)
                {
                    ModelPath modelPath = fileDialog.GetSelectedModelPath();
                    string filePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);

                    // Process the file
                    using (Transaction transaction = new Transaction(doc, "Import Grids"))
                    {
                        transaction.Start();
                        int importedCount = ImportGridsFromJson(doc, filePath);
                        transaction.Commit();

                        TaskDialog.Show("Import Complete", $"Successfully imported {importedCount} grids.");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private int ImportGridsFromJson(Document doc, string filePath)
        {
            int count = 0;
            var model = JsonConverter.LoadFromFile(filePath);

            foreach (var jsonGrid in model.Model.Grids)
            {

                try
                {
                    // Convert JSON grid points to Revit XYZ
                    XYZ startPoint = ConvertToRevitCoordinates(jsonGrid.StartPoint);
                    XYZ endPoint = ConvertToRevitCoordinates(jsonGrid.EndPoint);

                    // Create line for grid
                    Line gridLine = Line.CreateBound(startPoint, endPoint);

                    // Create grid in Revit
                    Grid revitGrid = Grid.Create(doc, gridLine);

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

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this grid but continue with the next one
                    Debug.WriteLine($"Error creating grid {jsonGrid.Name}: {ex.Message}");
                }
            }

            return count;
        }

        private XYZ ConvertToRevitCoordinates(GridPoint point)
        {
            // Convert from the JSON units (inches) to Revit's internal units (feet)
            double x = point.X / 12.0;
            double y = point.Y / 12.0;
            double z = point.Z / 12.0;

            return new XYZ(x, y, z);
        }

        internal static PushButtonData GetButtonData()
        {
            // use this method to define the properties for this command in the Revit ribbon
            string buttonInternalName = "btnCommand1";
            string buttonTitle = "Button 1";

            ButtonDataClass myButtonData1 = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                JSON_Connectors.Properties.Resources.Blue_32,
                JSON_Connectors.Properties.Resources.Blue_16,
                "This is a tooltip for Button 1");

            return myButtonData1.Data;
        }
    }
}
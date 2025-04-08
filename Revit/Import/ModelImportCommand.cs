using System;
using System.Diagnostics;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using Revit.Utilities;

namespace Revit.Import
{
    // External command for importing a complete structural model from JSON

    [Transaction(TransactionMode.Manual)]
    public class ModelImportCommand : IExternalCommand
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
                fileDialog.Title = "Select JSON Model File";

                if (fileDialog.Show() != ItemSelectionDialogResult.Canceled)
                {
                    ModelPath modelPath = fileDialog.GetSelectedModelPath();
                    string filePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);

                    // Import the model
                    ImportManager importManager = new ImportManager(doc, uiApp);
                    int importedCount = importManager.ImportFromJson(filePath);

                    TaskDialog.Show("Import Complete", $"Successfully imported model with {importedCount} elements.");

                    return Result.Succeeded;
                }

                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "btnImportModel";
            string buttonTitle = "Import Model";

            ButtonDataClass myButtonData = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                JSON_Connectors.Properties.Resources.Green_32,
                JSON_Connectors.Properties.Resources.Green_16,
                "Import a complete structural model from JSON");

            return myButtonData.Data;
        }
    }
}
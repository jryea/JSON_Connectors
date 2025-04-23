using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Revit.Utilities;
using RAM;

namespace Revit.Import
{
    [Transaction(TransactionMode.Manual)]
    public class RAMImportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // Show file dialog to select RAM file
                FileOpenDialog fileDialog = new FileOpenDialog("RAM Files (*.rss)|*.rss");
                fileDialog.Title = "Select RAM Model File";

                if (fileDialog.Show() != ItemSelectionDialogResult.Canceled)
                {
                    ModelPath ramModelPath = fileDialog.GetSelectedModelPath();
                    string ramFilePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(ramModelPath);

                    // Get the directory part of the RAM file path
                    string importDirectory = Path.GetDirectoryName(ramFilePath);
                    string importFileName = Path.GetFileNameWithoutExtension(ramFilePath);

                    // Create a temporary JSON file path
                    string tempJsonPath = Path.Combine(importDirectory, $"{importFileName}.json");

                    // Convert RAM to JSON using RAMExporter
                    RAMExporter ramExporter = new RAMExporter();
                    var conversionResult = ramExporter.ConvertRAMToJSON(ramFilePath);

                    if (!conversionResult.Success)
                    {
                        TaskDialog.Show("RAM Import Error", $"Failed to convert RAM file: {conversionResult.Message}");
                        return Result.Failed;
                    }

                    // Save the JSON output to a temporary file
                    File.WriteAllText(tempJsonPath, conversionResult.JsonOutput);

                    // Import the JSON model
                    ImportManager importManager = new ImportManager(doc, uiApp);
                    int importedCount = importManager.ImportFromJson(tempJsonPath);

                    TaskDialog.Show("Import Complete", $"Successfully imported RAM model with {importedCount} elements.");
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

        internal static System.Drawing.Bitmap ByteArrayToBitmap(byte[] byteArray)
        {
            using (var ms = new System.IO.MemoryStream(byteArray))
            {
                return new System.Drawing.Bitmap(ms);
            }
        }

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "btnImportRAM";
            string buttonTitle = "Import RAM";

            ButtonDataClass myButtonData = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_32),
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_16),
                "Import a RAM structural model into Revit");

            return myButtonData.Data;
        }
    }
}
using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Utilities;
using ETABS;
using Microsoft.Win32;

namespace Revit.Export
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ETABSExportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // Show save dialog for ETABS e2k file
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Title = "Save ETABS Model File";
                saveDialog.Filter = "ETABS Files (*.e2k)|*.e2k";
                saveDialog.DefaultExt = ".e2k";
                saveDialog.FileName = doc.Title;

                if (saveDialog.ShowDialog() != true)
                    return Result.Cancelled;

                string e2kFilePath = saveDialog.FileName;

                // Get the directory part of the e2k file path
                string exportDirectory = Path.GetDirectoryName(e2kFilePath);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(e2kFilePath);    

                // Create a temporary JSON file path
                string tempJsonPath = Path.Combine(exportDirectory, $"{fileNameWithoutExtension}.json");

                // Export the model to JSON
                ExportManager exportManager = new ExportManager(doc, uiApp);
                int exportedCount = exportManager.ExportToJson(tempJsonPath);

                if (exportedCount == 0)
                {
                    TaskDialog.Show("Export Error", "Failed to export model to JSON format.");
                    return Result.Failed;
                }

                // Convert JSON to ETABS e2k format
                try
                {
                    // Load the JSON file content
                    string jsonContent = File.ReadAllText(tempJsonPath);

                    // Create the converter and process the model
                    var converter = new ETABSImport();
                    string e2kContent = converter.ProcessModel(jsonContent, null, null);

                    // Save the E2K content to file
                    File.WriteAllText(e2kFilePath, e2kContent);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("ETABS Export Error", $"Failed to convert to ETABS format: {ex.Message}");
                    return Result.Failed;
                }

                TaskDialog.Show("Export Complete", $"Successfully exported model with {exportedCount} elements to ETABS format.");
                return Result.Succeeded;
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
            string buttonInternalName = "btnExportETABS";
            string buttonTitle = "Export to ETABS";

            ButtonDataClass myButtonData = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_32),
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_16),
                "Export the current structural model to ETABS format");

            return myButtonData.Data;
        }
    }
}
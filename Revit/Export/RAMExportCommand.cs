using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RAM;
using Revit.Utilities;

namespace Revit.Export
{
    [Transaction(TransactionMode.ReadOnly)]
    public class RAMExportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // Show save dialog for RAM file
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Title = "Save RAM Model File";
                saveDialog.Filter = "RAM Files (*.rss)|*.rss";
                saveDialog.DefaultExt = ".rss";
                saveDialog.FileName = doc.Title;

                if (saveDialog.ShowDialog() != true)
                    return Result.Cancelled;

                string ramFilePath = saveDialog.FileName;

                // Create a temporary JSON file path
                string tempJsonPath = Path.Combine(Path.GetTempPath(), $"Revit_Export_{Guid.NewGuid()}.json");

                // Export the model to JSON
                ExportManager exportManager = new ExportManager(doc, uiApp);
                int exportedCount = exportManager.ExportToJson(tempJsonPath);

                if (exportedCount == 0)
                {
                    TaskDialog.Show("Export Error", "Failed to export model to JSON format.");
                    return Result.Failed;
                }

                // Convert JSON to RAM
                RAMImporter ramImporter = new RAMImporter();
                var conversionResult = ramImporter.ConvertJSONFileToRAM(tempJsonPath, ramFilePath);

                // Delete the temporary file
                try { File.Delete(tempJsonPath); } catch { }

                if (!conversionResult.Success)
                {
                    TaskDialog.Show("RAM Export Error", $"Failed to convert to RAM format: {conversionResult.Message}");
                    return Result.Failed;
                }

                TaskDialog.Show("Export Complete", $"Successfully exported model with {exportedCount} elements to RAM format.");
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
            string buttonInternalName = "btnExportRAM";
            string buttonTitle = "Export to RAM";

            ButtonDataClass myButtonData = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_32),
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_16),
                "Export the current structural model to RAM format");

            return myButtonData.Data;
        }
    }
}
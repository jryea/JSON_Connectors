using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Revit.Utilities;
using Revit;

namespace Revit.Export
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ModelExportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // Show file dialog using Revit API
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Title = "Save JSON Model File";
                saveDialog.Filter = "JSON Files (*.json)|*.json";
                saveDialog.DefaultExt = ".json";
                saveDialog.FileName = doc.Title;

                if (saveDialog.ShowDialog() != true)
                    return Result.Cancelled;

                string filePath = saveDialog.FileName;

                // Export the model
                ExportManager exportManager = new ExportManager(doc, uiApp);
                int exportedCount = exportManager.ExportToJson(filePath);

                TaskDialog.Show("Export Complete", $"Successfully exported model with {exportedCount} elements.");

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
            string buttonInternalName = "btnExportModel";
            string buttonTitle = "Export Model";

            ButtonDataClass myButtonData = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_32),
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_16),
                "Export a complete structural model to JSON");

            return myButtonData.Data;
        }
    }
}
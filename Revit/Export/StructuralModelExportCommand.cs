using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Views;
using Revit.Utilities;

namespace Revit.Export
{
    [Transaction(TransactionMode.ReadOnly)]
    public class StructuralModelExportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Show the custom export dialog
                ExportStructuralModelWindow exportWindow = new ExportStructuralModelWindow(uiApp);
                bool? dialogResult = exportWindow.ShowDialog();

                // If dialog was completed successfully, result will be true
                if (dialogResult.HasValue && dialogResult.Value)
                {
                    return Result.Succeeded;
                }

                // If dialog was canceled, return cancelled
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error",
                    $"An error occurred while exporting structural model: {ex.Message}");
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
            string buttonInternalName = "btnExportStructuralModel";
            string buttonTitle = "Export Model";

            ButtonDataClass myButtonData = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_32),
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_16),
                "Export a complete structural model to multiple formats");

            return myButtonData.Data;
        }
    }
}
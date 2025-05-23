using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Views;
using Revit.Utilities;

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
                // Show the custom import dialog
                ImportStructuralModelWindow importWindow = new ImportStructuralModelWindow(uiApp);
                bool? dialogResult = importWindow.ShowDialog();

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
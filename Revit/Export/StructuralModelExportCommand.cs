using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Utilities;
using Revit.Views;

namespace Revit.Export
{
    /// <summary>
    /// Simplified structural model export command using the unified export architecture
    /// </summary>
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
                // Use existing export dialog - no changes to UI
                var exportWindow = new ExportStructuralModelWindow(uiApp);
                bool? dialogResult = exportWindow.ShowDialog();

                if (dialogResult.HasValue && dialogResult.Value)
                {
                    return Result.Succeeded;
                }

                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Export Error",
                    $"An error occurred while exporting structural model: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Keep existing method name and signature
        /// </summary>
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
                "Export structural model to multiple formats");

            return myButtonData.Data;
        }

        internal static System.Drawing.Bitmap ByteArrayToBitmap(byte[] byteArray)
        {
            using (var ms = new System.IO.MemoryStream(byteArray))
            {
                return new System.Drawing.Bitmap(ms);
            }
        }
    }
}
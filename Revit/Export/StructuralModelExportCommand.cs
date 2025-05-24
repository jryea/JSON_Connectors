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
                // Show the export dialog using the simplified view model
                var exportWindow = new SimplifiedExportStructuralModelWindow(uiApp);
                bool? dialogResult = exportWindow.ShowDialog();

                // Return result based on dialog outcome
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
        /// Gets the button data for the ribbon
        /// </summary>
        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "btnExportStructuralModelUnified";
            string buttonTitle = "Export Model";

            ButtonDataClass myButtonData = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_32),
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_16),
                "Export structural model to multiple formats using unified architecture");

            return myButtonData.Data;
        }

        /// <summary>
        /// Converts byte array to bitmap for button icons
        /// </summary>
        internal static System.Drawing.Bitmap ByteArrayToBitmap(byte[] byteArray)
        {
            using (var ms = new System.IO.MemoryStream(byteArray))
            {
                return new System.Drawing.Bitmap(ms);
            }
        }
    }
}
using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Revit.Utilities;
using ETABS;
using ETABS.Utilities;

namespace Revit.Import
{
    [Transaction(TransactionMode.Manual)]
    public class ETABSImportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // Show file dialog to select ETABS file
                FileOpenDialog fileDialog = new FileOpenDialog("ETABS Files (*.e2k)|*.e2k");
                fileDialog.Title = "Select ETABS Model File";

                if (fileDialog.Show() != ItemSelectionDialogResult.Canceled)
                {
                    ModelPath etabsModelPath = fileDialog.GetSelectedModelPath();
                    string etabsFilePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(etabsModelPath);

                    // Create a temporary JSON file path
                    string tempJsonPath = Path.Combine(Path.GetTempPath(), $"ETABS_Import_{Guid.NewGuid()}.json");

                    try
                    {
                        // Read E2K file content
                        string e2kContent = File.ReadAllText(etabsFilePath);

                        // Convert ETABS to JSON
                        var converter = new ETABSToGrasshopper();
                        string jsonContent = converter.ProcessE2K(e2kContent);

                        // Save JSON to temporary file
                        File.WriteAllText(tempJsonPath, jsonContent);

                        // Import the JSON model into Revit
                        ImportManager importManager = new ImportManager(doc, uiApp);
                        int importedCount = importManager.ImportFromJson(tempJsonPath);

                        TaskDialog.Show("Import Complete", $"Successfully imported ETABS model with {importedCount} elements.");
                        return Result.Succeeded;
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("ETABS Import Error", $"Failed to import ETABS model: {ex.Message}");
                        return Result.Failed;
                    }
                    finally
                    {
                        // Clean up the temporary file
                        try { File.Delete(tempJsonPath); } catch { }
                    }
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
            string buttonInternalName = "btnImportETABS";
            string buttonTitle = "Import ETABS";

            ButtonDataClass myButtonData = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_32),
                ByteArrayToBitmap(Revit.Properties.Resources.IMEG_16),
                "Import an ETABS structural model into Revit");

            return myButtonData.Data;
        }
    }
}
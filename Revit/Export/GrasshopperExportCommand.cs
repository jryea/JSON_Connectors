using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using System.IO;

namespace Revit.Export
{
    [Transaction(TransactionMode.ReadOnly)]
    public class RevitToGrasshopperCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            // Show export dialog
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Title = "Export to Grasshopper";
            saveDialog.Filter = "JSON Files (*.json)|*.json";
            saveDialog.DefaultExt = ".json";
            saveDialog.FileName = doc.Title;

            if (saveDialog.ShowDialog() != true)
                return Result.Cancelled;

            string jsonFilePath = saveDialog.FileName;
            string exportFolder = Path.GetDirectoryName(jsonFilePath);
            string baseName = Path.GetFileNameWithoutExtension(jsonFilePath);

            // Create a dedicated subfolder for all files
            string projectFolder = Path.Combine(exportFolder, baseName);
            Directory.CreateDirectory(projectFolder);

            string jsonPath = Path.Combine(projectFolder, baseName + ".json");
            string dwgFolder = Path.Combine(projectFolder, "CAD");
            Directory.CreateDirectory(dwgFolder);

            // Execute the exporter
            GrasshopperExporter exporter = new GrasshopperExporter(doc, uiApp);
            exporter.ExportAll(jsonPath, dwgFolder);

            return Result.Succeeded;
        }
    }   
}
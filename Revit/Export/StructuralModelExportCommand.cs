using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using Core.Models.ModelLayout;
using ETABS;
using RAM;
using Revit.Export.Models;
using Revit.Utilities;
using Revit.Views;

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

                // If dialog was completed successfully, the window will handle export
                // and return Result.Succeeded via the ViewModel
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

        /// <summary>
        /// Performs the export operation based on selected export format
        /// </summary>
        public static void PerformExport(UIApplication uiApp, Document doc, string outputPath,
                                        bool exportToETABS, bool exportToRAM, bool exportToGrasshopper,
                                        Dictionary<string, bool> elementFilters,
                                        Dictionary<string, bool> materialFilters,
                                        List<ElementId> selectedLevelIds,
                                        ElementId baseLevelId,
                                        List<Core.Models.ModelLayout.Level> coreLevels = null,
                                        List<Core.Models.ModelLayout.FloorType> floorTypes = null,
                                        Dictionary<string, ElementId> floorTypeToViewMap = null)
        {
            try
            {
                // Get the directory and file name
                string directory = Path.GetDirectoryName(outputPath);
                string fileName = Path.GetFileNameWithoutExtension(outputPath);

                // Always do a JSON export for all formats (either as final format or intermediate)
                string jsonPath;

                if (exportToGrasshopper)
                {
                    jsonPath = outputPath; // Use the direct path for Grasshopper
                }
                else
                {
                    // For ETABS/RAM, the JSON is an intermediate file
                    jsonPath = Path.Combine(directory, fileName + ".json");
                }

                int exportedCount = 0;

                if (exportToGrasshopper && coreLevels != null && floorTypes != null && floorTypeToViewMap != null)
                {
                    // Create the CAD folder path
                    string dwgFolder = Path.Combine(directory, "CAD");
                    Directory.CreateDirectory(dwgFolder);

                    // Use Grasshopper specific export
                    GrasshopperExporter exporter = new GrasshopperExporter(doc, uiApp);
                    exporter.ExportWithFloorTypeViewMappings(
                        jsonPath,
                        dwgFolder,
                        floorTypes,
                        coreLevels,
                        null, // No reference point needed
                        floorTypeToViewMap,
                        selectedLevelIds
                    );

                    TaskDialog.Show("Export Complete",
                        $"Successfully exported model to Grasshopper format at {outputPath}");
                }
                else
                {
                    // Use standard export
                    ExportManager exportManager = new ExportManager(doc, uiApp);
                    exportedCount = exportManager.ExportToJson(jsonPath, elementFilters, materialFilters,
                                                             selectedLevelIds, baseLevelId);

                    // For ETABS, convert to E2K
                    if (exportToETABS)
                    {
                        string e2kPath = Path.Combine(directory, fileName + ".e2k");

                        try
                        {
                            // Load the JSON file content
                            string jsonContent = File.ReadAllText(jsonPath);

                            // Create the converter and process the model
                            var converter = new ETABSImport();
                            string e2kContent = converter.ProcessModel(jsonContent, null, null);

                            // Save the E2K content to file
                            File.WriteAllText(e2kPath, e2kContent);

                            TaskDialog.Show("Export Complete",
                                $"Successfully exported {exportedCount} elements to ETABS format at {e2kPath}");
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("ETABS Export Error",
                                $"Failed to convert to ETABS format: {ex.Message}");
                            throw;
                        }
                    }

                    // For RAM, convert to RSS
                    else if (exportToRAM)
                    {
                        string ramPath = Path.Combine(directory, fileName + ".rss");

                        try
                        {
                            // Convert JSON to RAM
                            RAMImporter ramImporter = new RAMImporter();
                            var conversionResult = ramImporter.ConvertJSONFileToRAM(jsonPath, ramPath);

                            if (!conversionResult.Success)
                            {
                                TaskDialog.Show("RAM Export Error",
                                    $"Failed to convert to RAM format: {conversionResult.Message}");
                                throw new Exception(conversionResult.Message);
                            }

                            TaskDialog.Show("Export Complete",
                                $"Successfully exported {exportedCount} elements to RAM format at {ramPath}");
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("RAM Export Error",
                                $"Failed to convert to RAM format: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Export Error", $"Error during export: {ex.Message}");
                throw;
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace Revit
{
    /// <summary>
    /// Implements the Revit add-in interface IExternalApplication
    /// </summary>
    public class RevitApplication : IExternalApplication
    {
        // Singleton instance
        internal static RevitApplication Instance { get; private set; }

        // Revit UI application
        internal UIControlledApplication UiControlledApplication { get; private set; }

        /// <summary>
        /// Implements the OnStartup method of IExternalApplication
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Store the application instance
                Instance = this;
                UiControlledApplication = application;

                // Create a custom ribbon tab
                string tabName = "JSON Connectors";
                application.CreateRibbonTab(tabName);

                // Create ribbon panels
                var importPanel = application.CreateRibbonPanel(tabName, "Import");
                var exportPanel = application.CreateRibbonPanel(tabName, "Export");

                // Get assembly path
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                // Add Grid Importer button to Import panel
                PushButtonData gridImportBtn = new PushButtonData(
                    "GridImporter",
                    "Import\nGrids",
                    assemblyPath,
                    "Revit.UI.GridImportCommand")
                {
                    ToolTip = "Import grids from JSON file",
                    LongDescription = "Import grid data from a JSON file into the current Revit model.",
                    LargeImage = GetEmbeddedImage("Revit.Resources.Icons.grid_import_32.png"),
                    Image = GetEmbeddedImage("Revit.Resources.Icons.grid_import_16.png")
                };

                importPanel.AddItem(gridImportBtn);

                // Add placeholder for future export commands (for completeness)
                PushButtonData gridExportBtn = new PushButtonData(
                    "GridExporter",
                    "Export\nGrids",
                    assemblyPath,
                    "Revit.UI.GridExportCommand") // Note: This class would need to be implemented
                {
                    ToolTip = "Export grids to JSON file",
                    LongDescription = "Export grid data from the current Revit model to a JSON file.",
                    // Using the same icon for now, would be replaced with export-specific icon
                    LargeImage = GetEmbeddedImage("Revit.Resources.Icons.grid_import_32.png"),
                    Image = GetEmbeddedImage("Revit.Resources.Icons.grid_import_16.png")
                };

                exportPanel.AddItem(gridExportBtn);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to initialize JSON Connectors: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Implements the OnShutdown method of IExternalApplication
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            // Clean up resources if needed

            return Result.Succeeded;
        }

        /// <summary>
        /// Gets embedded image resource
        /// </summary>
        private BitmapSource GetEmbeddedImage(string resourceName)
        {
            try
            {
                // Get assembly
                Assembly assembly = Assembly.GetExecutingAssembly();

                // Open resource stream
                Stream stream = assembly.GetManifestResourceStream(resourceName);

                // Convert to bitmap
                if (stream != null)
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = stream;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }

                return null;
            }
            catch
            {
                // In case of error, return null (no image)
                return null;
            }
        }
    }
}
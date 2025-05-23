#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows.Markup;
using Revit.Import;
using Revit.Export;
using RU = Revit.Utilities;
using UIFramework;


#endregion

namespace Revit
{
    internal class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            // Create ribbon tab
            string tabName = "IMEG Connectors";
            try
            {
                app.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                Debug.Print("Tab already exists.");
            }

            // Create software export/import panels
            RibbonPanel modelPanel = RU.Utils.CreateRibbonPanel(app, tabName, "Model");
            RibbonPanel ETABSPanel = RU.Utils.CreateRibbonPanel(app, tabName, "ETABS");
            RibbonPanel RAMPanel = RU.Utils.CreateRibbonPanel(app, tabName, "RAM");

            // Create model button data
            PushButtonData btnModelImport = ModelImportCommand.GetButtonData();
            PushButtonData btnModelExport = ModelExportCommand.GetButtonData();
            PushButtonData btnGrasshopperExport = GrasshopperExportCommand.GetButtonData();
            PushButtonData btnStructuralModelExport = StructuralModelExportCommand.GetButtonData();

            // Create RAM button data
            PushButtonData btnRAMImport = RAMImportCommand.GetButtonData();
            PushButtonData btnRAMExport = RAMExportCommand.GetButtonData();

            // Create ETABS button data
            PushButtonData btnETABSImport = ETABSImportCommand.GetButtonData();
            PushButtonData btnETABSExport = ETABSExportCommand.GetButtonData();

            // Add buttons to model panel
            PushButton buttonModelImport = modelPanel.AddItem(btnModelImport) as PushButton;
            PushButton buttonModelExport = modelPanel.AddItem(btnModelExport) as PushButton;
            PushButton buttonGrasshopperExport = modelPanel.AddItem(btnGrasshopperExport) as PushButton;
            PushButton buttonStructuralModelExport = modelPanel.AddItem(btnStructuralModelExport) as PushButton;
            buttonStructuralModelExport.LargeImage = btnStructuralModelExport.LargeImage;
            buttonStructuralModelExport.Image = btnStructuralModelExport.Image;

            // Add buttons to RAM panel
            PushButton buttonRAMImport = RAMPanel.AddItem(btnRAMImport) as PushButton;
            PushButton buttonRAMExport = RAMPanel.AddItem(btnRAMExport) as PushButton;

            // In App.cs OnStartup method
            PushButtonData btnStructuralModelImport = ImportStructuralModelCommand.GetButtonData();
            PushButton buttonStructuralModelImport = modelPanel.AddItem(btnStructuralModelImport) as PushButton;

            // Add buttons to ETABS panel
            PushButton buttonETABSImport = ETABSPanel.AddItem(btnETABSImport) as PushButton;
            PushButton buttonETABSExport = ETABSPanel.AddItem(btnETABSExport) as PushButton;

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }


    }
}
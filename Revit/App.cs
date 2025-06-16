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

            // Create model button data
            PushButtonData btnStructuralModelExport = StructuralModelExportCommand.GetButtonData();
            PushButtonData btnStructuralModelImport = ImportStructuralModelCommand.GetButtonData();

            // Add buttons to model panel
            PushButton buttonStructuralModelExport = modelPanel.AddItem(btnStructuralModelExport) as PushButton;
            PushButton buttonStructuralModelImport = modelPanel.AddItem(btnStructuralModelImport) as PushButton;

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }


    }
}
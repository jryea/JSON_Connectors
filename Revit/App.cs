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
using RU = Revit.Utilities;  

#endregion

namespace Revit
{
    internal class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            // 1. Create ribbon tab
            string tabName = "JSON Connectors";
            try
            {
                app.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                Debug.Print("Tab already exists.");
            }

            // In App.cs, add the new button:
            RibbonPanel panel = RU.Utils.CreateRibbonPanel(app, tabName, "Import");

            // Create button data instances
            PushButtonData btnModelImport = ModelImportCommand.GetButtonData();
            PushButtonData btnModelExport = Export.ModelExportCommand.GetButtonData();

            // Create buttons
            PushButton buttonModelImport = panel.AddItem(btnModelImport) as PushButton;
            PushButton buttonModelExport = panel.AddItem(btnModelExport) as PushButton;

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }


    }
}

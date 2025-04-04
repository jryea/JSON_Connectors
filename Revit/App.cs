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
using RU = Revit.Utils;  

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
            PushButtonData btnGridImport = GridImportCommand.GetButtonData();
            PushButtonData btnModelImport = ModelImportCommand.GetButtonData();

            // Create buttons
            PushButton buttonGridImport = panel.AddItem(btnGridImport) as PushButton;
            PushButton buttonModelImport = panel.AddItem(btnModelImport) as PushButton;

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }


    }
}

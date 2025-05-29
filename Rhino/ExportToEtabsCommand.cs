using Rhino;
using Rhino.Commands;
using Rhino.UI;
using System;
using System.Linq;
using Grasshopper.Kernel.Special;

namespace StructuralSetup.Commands
{
    [System.Runtime.InteropServices.Guid("a3f8d2c1-4b7e-9f2d-8c5a-1e6b3d9f4a7c")]
    public partial class ExportToEtabsCommand : Command
    {
        public ExportToEtabsCommand()
        {
            Instance = this;
        }

        public static ExportToEtabsCommand Instance { get; private set; }

        public override string EnglishName => "ExportToEtabs";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            string filePath = GetE2KOutputFilePath();
            if (string.IsNullOrEmpty(filePath)) return Result.Cancel;

            var gh = Grasshopper.Instances.ActiveCanvas?.Document;
            if (gh == null)
            {
                RhinoApp.WriteLine("Error: No active Grasshopper document found.");
                return Result.Failure;
            }

            try
            {
                // Set both E2K and JSON paths
                string jsonPath = System.IO.Path.ChangeExtension(filePath, ".json");
                SetFilePath(gh, filePath, "OutputPath");
                SetFilePath(gh, jsonPath, "JSONOutputPath");

                bool triggered = TriggerExport(gh, "ETABSTrigger");
                if (!triggered) return Result.Failure;

                RhinoApp.WriteLine($"ETABS export triggered successfully!");
                RhinoApp.WriteLine($"E2K file: {filePath}");
                RhinoApp.WriteLine($"JSON file: {jsonPath}");

                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error: {ex.Message}");
                return Result.Failure;
            }
        }

        private string GetE2KOutputFilePath()
        {
            using (var dialog = new Eto.Forms.SaveFileDialog
            {
                Title = "Save ETABS E2K File",
                Filters = { new Eto.Forms.FileFilter("ETABS Files", "*.e2k") }
            })
            {
                if (dialog.ShowDialog(RhinoEtoApp.MainWindow) == Eto.Forms.DialogResult.Ok)
                {
                    string filePath = dialog.FileName;
                    if (!System.IO.Path.HasExtension(filePath))
                        filePath += ".e2k";
                    return filePath;
                }
            }
            return null;
        }
    }

    [System.Runtime.InteropServices.Guid("b4e9f3d2-5c8a-0e3f-9d6b-2f7c4e8b5a9d")]
    public partial class ExportToRAMCommand : Command
    {
        public ExportToRAMCommand()
        {
            Instance = this;
        }

        public static ExportToRAMCommand Instance { get; private set; }

        public override string EnglishName => "ExportToRAM";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            string filePath = GetRSSOutputFilePath();
            if (string.IsNullOrEmpty(filePath)) return Result.Cancel;

            var gh = Grasshopper.Instances.ActiveCanvas?.Document;
            if (gh == null)
            {
                RhinoApp.WriteLine("Error: No active Grasshopper document found.");
                return Result.Failure;
            }

            try
            {
                // Set both RSS and JSON paths
                string jsonPath = System.IO.Path.ChangeExtension(filePath, ".json");
                SetFilePath(gh, filePath, "OutputPath");
                SetFilePath(gh, jsonPath, "JSONOutputPath");

                bool triggered = TriggerExport(gh, "RAMTrigger");
                if (!triggered) return Result.Failure;

                RhinoApp.WriteLine($"RAM export triggered successfully!");
                RhinoApp.WriteLine($"RSS file: {filePath}");
                RhinoApp.WriteLine($"JSON file: {jsonPath}");

                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error: {ex.Message}");
                return Result.Failure;
            }
        }

        private string GetRSSOutputFilePath()
        {
            using (var dialog = new Eto.Forms.SaveFileDialog
            {
                Title = "Save RAM RSS File",
                Filters = { new Eto.Forms.FileFilter("RAM Files", "*.rss") }
            })
            {
                if (dialog.ShowDialog(RhinoEtoApp.MainWindow) == Eto.Forms.DialogResult.Ok)
                {
                    string filePath = dialog.FileName;
                    if (!System.IO.Path.HasExtension(filePath))
                        filePath += ".rss";
                    return filePath;
                }
            }
            return null;
        }
    }

    [System.Runtime.InteropServices.Guid("c5f0e4d3-6d9c-1f4e-0b7a-3f8c5e9d2a6b")]
    public partial class ExportToRevitCommand : Command
    {
        public ExportToRevitCommand()
        {
            Instance = this;
        }

        public static ExportToRevitCommand Instance { get; private set; }

        public override string EnglishName => "ExportToRevit";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            string filePath = GetJSONOutputFilePath();
            if (string.IsNullOrEmpty(filePath)) return Result.Cancel;

            var gh = Grasshopper.Instances.ActiveCanvas?.Document;
            if (gh == null)
            {
                RhinoApp.WriteLine("Error: No active Grasshopper document found.");
                return Result.Failure;
            }

            try
            {
                // Only set JSON path for Revit export
                SetFilePath(gh, filePath, "JSONOutputPath");

                bool triggered = TriggerExport(gh, "RevitTrigger");
                if (!triggered) return Result.Failure;

                RhinoApp.WriteLine($"Revit export triggered successfully!");
                RhinoApp.WriteLine($"JSON file: {filePath}");

                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error: {ex.Message}");
                return Result.Failure;
            }
        }

        private string GetJSONOutputFilePath()
        {
            using (var dialog = new Eto.Forms.SaveFileDialog
            {
                Title = "Save JSON Model File for Revit",
                Filters = { new Eto.Forms.FileFilter("JSON Files", "*.json") }
            })
            {
                if (dialog.ShowDialog(RhinoEtoApp.MainWindow) == Eto.Forms.DialogResult.Ok)
                {
                    string filePath = dialog.FileName;
                    if (!System.IO.Path.HasExtension(filePath))
                        filePath += ".json";
                    return filePath;
                }
            }
            return null;
        }
    }

    // Shared helper methods
    public static class ExportHelpers
    {
        public static bool SetFilePath(Grasshopper.Kernel.GH_Document gh, string filePath, string componentNickname)
        {
            var textPanel = gh.Objects.FirstOrDefault(obj =>
                obj is GH_Panel &&
                obj.NickName.Equals(componentNickname, StringComparison.OrdinalIgnoreCase)) as GH_Panel;

            if (textPanel != null)
            {
                textPanel.UserText = filePath;
                textPanel.ExpireSolution(true);
                return true;
            }
            return false;
        }

        public static bool TriggerExport(Grasshopper.Kernel.GH_Document gh, string toggleNickname)
        {
            var toggle = gh.Objects.FirstOrDefault(obj =>
                obj is GH_BooleanToggle &&
                obj.NickName.Equals(toggleNickname, StringComparison.OrdinalIgnoreCase)) as GH_BooleanToggle;

            if (toggle != null)
            {
                RhinoApp.WriteLine($"Found {toggleNickname}, activating...");

                toggle.Value = true;
                toggle.ExpireSolution(true);
                gh.NewSolution(false);

                System.Threading.Thread.Sleep(100);
                toggle.Value = false;
                toggle.ExpireSolution(true);
                gh.NewSolution(false);

                return true;
            }

            RhinoApp.WriteLine($"{toggleNickname} not found!");
            return false;
        }
    }
}

// Extension methods to use shared helpers
namespace StructuralSetup.Commands
{
    public partial class ExportToEtabsCommand
    {
        private bool SetFilePath(Grasshopper.Kernel.GH_Document gh, string filePath, string componentNickname) =>
            ExportHelpers.SetFilePath(gh, filePath, componentNickname);
        private bool TriggerExport(Grasshopper.Kernel.GH_Document gh, string toggleNickname) =>
            ExportHelpers.TriggerExport(gh, toggleNickname);
    }

    public partial class ExportToRAMCommand
    {
        private bool SetFilePath(Grasshopper.Kernel.GH_Document gh, string filePath, string componentNickname) =>
            ExportHelpers.SetFilePath(gh, filePath, componentNickname);
        private bool TriggerExport(Grasshopper.Kernel.GH_Document gh, string toggleNickname) =>
            ExportHelpers.TriggerExport(gh, toggleNickname);
    }

    public partial class ExportToRevitCommand
    {
        private bool SetFilePath(Grasshopper.Kernel.GH_Document gh, string filePath, string componentNickname) =>
            ExportHelpers.SetFilePath(gh, filePath, componentNickname);
        private bool TriggerExport(Grasshopper.Kernel.GH_Document gh, string toggleNickname) =>
            ExportHelpers.TriggerExport(gh, toggleNickname);
    }
}
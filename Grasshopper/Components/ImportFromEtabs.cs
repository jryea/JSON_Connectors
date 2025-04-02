using System;
using System.IO;
using Grasshopper.Kernel;
using ETABS.Export;
using ETABS;
using System.Collections.Generic;
using Grasshopper.Components.Core;

namespace Grasshopper.Components
{
    public class ImportFromEtabs : ComponentBase
    {
        public ImportFromEtabs()
            : base("Import From Etabs", "E2K2J",
                "Converts ETABS E2K format to JSON model",
                "IMEG", "Import")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("E2K Content/Path", "E", "E2K file content or path", GH_ParamAccess.item);
            pManager.AddTextParameter("Output Path", "P", "Path to save JSON file (optional)", GH_ParamAccess.item);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON representation of the model", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if import was successful", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string e2kInput = string.Empty;
            string outputPath = string.Empty;

            if (!DA.GetData(0, ref e2kInput)) return;
            DA.GetData(1, ref outputPath);

            if (string.IsNullOrWhiteSpace(e2kInput))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "E2K input is empty or invalid");
                DA.SetData(0, "Error: E2K input is empty or invalid");
                DA.SetData(1, false);
                return;
            }

            try
            {
                // Check if input is a file path or content
                string e2kContent = File.Exists(e2kInput) ? File.ReadAllText(e2kInput) : e2kInput;

                var converter = new ETABSToGrasshopper();
                string jsonContent = converter.ProcessE2K(e2kContent, outputPath);

                DA.SetData(0, jsonContent);
                DA.SetData(1, true);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                DA.SetData(0, $"Error: {ex.Message}");
                DA.SetData(1, false);
            }
        }
        public override Guid ComponentGuid
        {
            get { return new Guid("395588db-9f39-4614-b7b9-a1f4f28ea285"); }
        }
    }
}
using System;
using System.IO;
using Grasshopper.Kernel;
using RAM;
using System.Collections.Generic;
using Grasshopper.Components.Core;

namespace Grasshopper.Components
{
    public class ExportToRAM : ComponentBase
    {
        public ExportToRAM()
            : base("Export To RAM", "J2RAM",
                "Converts JSON model to RAM structural model",
                "IMEG", "Import/Export")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON Input", "J", "JSON model content or path to JSON file", GH_ParamAccess.item);
            pManager.AddTextParameter("Output Path", "P", "Path to save RAM file (.rss)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "Result message", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if export was successful", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string jsonInput = string.Empty;
            string outputPath = string.Empty;

            if (!DA.GetData(0, ref jsonInput)) return;
            if (!DA.GetData(1, ref outputPath)) return;

            if (string.IsNullOrWhiteSpace(jsonInput))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "JSON input is empty or invalid");
                DA.SetData(0, "Error: JSON input is empty or invalid");
                DA.SetData(1, false);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Output path is required");
                DA.SetData(0, "Error: Output path is required");
                DA.SetData(1, false);
                return;
            }

            // Check file extension
            string extension = Path.GetExtension(outputPath).ToLower();
            if (extension != ".rss")
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Output file should have .rss extension");
            }

            try
            {
                var converter = new JSONToRAMConverter();

                // Determine if the input is a file path or direct JSON content
                bool isFilePath = File.Exists(jsonInput) && jsonInput.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

                // Call the appropriate converter method based on input type
                (bool Success, string Message) result;

                if (isFilePath == true)
                {
                    result = converter.ConvertJSONFileToRAM(jsonInput, outputPath);
                }
                else 
                {
                    result = converter.ConvertJSONStringToRAM(jsonInput, outputPath);
                }

                DA.SetData(0, result.Message);
                DA.SetData(1, result.Success);
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
            get { return new Guid("6b2a7c58-e7d1-43e5-8bc9-a5c85e9a507e"); }
        }
    }
}
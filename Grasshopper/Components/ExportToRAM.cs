using System;
using System.IO;
using Grasshopper.Kernel;
using RAM;
using System.Collections.Generic;
using Grasshopper.Components.Core;
using static System.Resources.ResXFileRef;

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
            pManager.AddTextParameter("JSON Path", "J", "JSON file content or path", GH_ParamAccess.item);
            pManager.AddTextParameter("Output Path", "P", "Path to save RAM file (.rmx, .rss)", GH_ParamAccess.item);
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
                // Check if input is a file path or content
                string jsonContent;
                if (File.Exists(jsonInput))
                {
                    jsonContent = File.ReadAllText(jsonInput);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Reading JSON from file: " + jsonInput);
                }
                else
                {
                    jsonContent = jsonInput;
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Using provided JSON content");
                }

                // Create temporary JSON file if needed
                string jsonFilePath;
                bool usingTempFile = false;
                if (File.Exists(jsonInput))
                {
                    jsonFilePath = jsonInput;
                }
                else
                {
                    // Create temp file for JSON content
                    jsonFilePath = Path.Combine(Path.GetTempPath(), "temp_model_" + Guid.NewGuid().ToString() + ".json");
                    File.WriteAllText(jsonFilePath, jsonContent);
                    usingTempFile = true;
                }

                // Convert JSON to RAM
                var converter = new JSONToRAMConverter();
                var result = converter.ConvertJSONToRAM(jsonFilePath, outputPath);
                bool success = result.Success;
                string message = result.Message;

                if (success)
                {
                    DA.SetData(0, "Successfully exported model to RAM: " + outputPath);
                    DA.SetData(1, true);
                }
                else
                {
                    DA.SetData(0, "Failed to export model to RAM: " + message);
                    DA.SetData(1, false);
                }
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
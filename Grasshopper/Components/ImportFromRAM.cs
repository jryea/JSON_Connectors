using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Grasshopper.Kernel;
using RAM;
using RAM.Utilities;
using Core.Models;
using Core.Models.ModelLayout;
using Core.Models.Elements;
using Core.Models.Properties;
using Core.Models.Geometry;
using Core.Models.Metadata;
using Core.Converters;

namespace Grasshopper.Components
{
    public class ImportFromRAM : GH_Component
    {
        public ImportFromRAM()
            : base("Import From RAM", "RAM2J",
                "Imports a RAM model and converts it to JSON",
                "IMEG", "Import/Export")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("RAM File Path", "R", "Path to RAM database file (.rss)", GH_ParamAccess.item);
            pManager.AddTextParameter("Output JSON Path", "J", "Path to save JSON file (optional)", GH_ParamAccess.item);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON representation of the model", GH_ParamAccess.item);
            pManager.AddTextParameter("Result", "R", "Result message", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if import was successful", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string ramFilePath = string.Empty;
            string outputJsonPath = string.Empty;

            if (!DA.GetData(0, ref ramFilePath)) return;
            DA.GetData(1, ref outputJsonPath); // Optional

            if (string.IsNullOrWhiteSpace(ramFilePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "RAM file path is empty or invalid");
                DA.SetData(1, "Error: RAM file path is empty or invalid");
                DA.SetData(2, false);
                return;
            }

            if (!File.Exists(ramFilePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "RAM file does not exist");
                DA.SetData(1, $"Error: RAM file does not exist at path: {ramFilePath}");
                DA.SetData(2, false);
                return;
            }

            try
            {
                var converter = new RAMToJSONConverter();
                (string JsonOutput, string Message, bool Success) result = converter.ConvertRAMToJSON(ramFilePath);

                // Save JSON to file if path provided
                if (Success && !string.IsNullOrWhiteSpace(outputJsonPath))
                {
                    try
                    {
                        File.WriteAllText(outputJsonPath, result.JsonOutput, Encoding.UTF8);
                        result.Message += $" JSON file saved to: {outputJsonPath}";
                    }
                    catch (Exception ex)
                    {
                        result.Message += $" Error saving JSON file: {ex.Message}";
                    }
                }

                DA.SetData(0, result.JsonOutput);
                DA.SetData(1, result.Message);
                DA.SetData(2, result.Success);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                DA.SetData(1, $"Error: {ex.Message}");
                DA.SetData(2, false);
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("8d4a7c58-e7d1-43e5-8bc9-a5c85e9a508f"); }
        }
    }
}
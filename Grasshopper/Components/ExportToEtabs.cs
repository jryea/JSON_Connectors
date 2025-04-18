using System;
using System.IO;
using Grasshopper.Kernel;
using ETABS.ToETABS;
using ETABS;
using System.Collections.Generic;
using Grasshopper.Components.Core;

namespace Grasshopper.Components
{
    public class ExportToEtabs : ComponentBase
    {
        public ExportToEtabs()
            : base("Export To Etabs", "E2K",
                "Converts JSON model to ETABS E2K format",
                "IMEG", "Import/Export")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON representation of the structural model", GH_ParamAccess.item);
            pManager.AddTextParameter("Output Path", "P", "Path to save the E2K file", GH_ParamAccess.item);
            pManager.AddTextParameter("E2K Sections", "E2K", "Custom E2K sections in format 'SECTION_NAME:CONTENT'", GH_ParamAccess.item);
            pManager[2].Optional = true; // Optional parameter for custom E2K sections
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "Result of the export operation", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if export was successful", GH_ParamAccess.item);
            pManager.AddTextParameter("E2K", "E2K", "ETABS E2K export", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get input data
            string jsonInput = string.Empty;
            string filePath = string.Empty;
            string customE2K = string.Empty;

            if (!DA.GetData(0, ref jsonInput)) return;
            if (!DA.GetData(1, ref filePath)) return;
            DA.GetData(2, ref customE2K);

            // Validate input
            if (string.IsNullOrWhiteSpace(jsonInput))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "JSON input is empty or invalid");
                DA.SetData(0, "Error: JSON input is empty or invalid");
                DA.SetData(1, false);
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Output path is empty or invalid");
                DA.SetData(0, "Error: Output path is empty or invalid");
                DA.SetData(1, false);
                return;
            }

            try
            {
                // Ensure the directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create exporter
                var converter = new GrasshopperToETABS();

                // Get both outputs from the same source
                string e2kContent = converter.ProcessModel(jsonInput, customE2K, filePath);
      
                // Set output
                DA.SetData(0, $"Successfully exported to {filePath}");
                DA.SetData(1, true);
                DA.SetData(2, e2kContent);
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
            get { return new Guid("01234567-89AB-CDEF-0123-456789ABCDEF"); }
        }
    }
}
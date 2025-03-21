using System;
using System.IO;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Core.Models;
//using ETABS.Core.Export;

namespace Grasshopper.Components.ETABS
{
    /// <summary>
    /// Component for exporting structural model to ETABS E2K format
    /// </summary>
    public class ExportModelToETABSComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ExportModelToETABS class.
        /// </summary>
        public ExportModelToETABSComponent()
          : base("Export to ETABS", "E2K",
              "Exports structural model to ETABS E2K format",
              "Structural", "Export")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Structural model to export", GH_ParamAccess.item);
            pManager.AddTextParameter("FilePath", "F", "Path to save E2K file", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Export", "E", "Trigger export when true", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "True if export was successful", GH_ParamAccess.item);
            pManager.AddTextParameter("Result", "R", "Export result message", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get input parameters
            BaseModel model = null;
            string filePath = string.Empty;
            bool export = false;

            if (!DA.GetData(0, ref model))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No structural model provided");
                return;
            }

            if (!DA.GetData(1, ref filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No file path provided");
                return;
            }

            DA.GetData(2, ref export);

            // If export is not triggered, just report ready status
            if (!export)
            {
                DA.SetData(0, false);
                DA.SetData(1, "Ready to export. Set Export to True to generate E2K file.");
                return;
            }

            // Validate file path
            if (string.IsNullOrWhiteSpace(filePath))
            {
                DA.SetData(0, false);
                DA.SetData(1, "Invalid file path");
                return;
            }

            // Ensure file extension is .e2k
            if (!filePath.ToLower().EndsWith(".e2k"))
                filePath += ".e2k";

            // Export model to E2K
            try
            {
                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // Export using E2KExporter
                var exporter = new E2KExport();
                exporter.ExportToE2K(model, filePath);

                DA.SetData(0, true);
                DA.SetData(1, $"Successfully exported model to {filePath}");
            }
            catch (Exception ex)
            {
                DA.SetData(0, false);
                DA.SetData(1, $"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add a custom icon here
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("A8F51E72-6B9C-48E3-B8CE-D72A3C452935"); }
        }
    }
}
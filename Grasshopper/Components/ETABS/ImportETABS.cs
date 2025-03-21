using System;
using System.IO;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Core.Models;
using ETABS.Core.Import;
using ETABS.Core.Export;

namespace Grasshopper.Components.ETABS
{
    /// <summary>
    /// Component for importing ETABS E2K format to structural model
    /// </summary>
    public class ImportModelFromETABSComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ImportModelFromETABS class.
        /// </summary>
        public ImportModelFromETABSComponent()
          : base("Import from ETABS", "FromE2K",
              "Imports structural model from ETABS E2K format",
              "Structural", "Import")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("FilePath", "F", "Path to E2K file", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Import", "I", "Trigger import when true", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Imported structural model", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if import was successful", GH_ParamAccess.item);
            pManager.AddTextParameter("Result", "R", "Import result message", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get input parameters
            string filePath = string.Empty;
            bool import = false;

            if (!DA.GetData(0, ref filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No file path provided");
                return;
            }

            DA.GetData(1, ref import);

            // If import is not triggered, just report ready status
            if (!import)
            {
                DA.SetData(1, false);
                DA.SetData(2, "Ready to import. Set Import to True to load E2K file.");
                return;
            }

            // Validate file path
            if (string.IsNullOrWhiteSpace(filePath))
            {
                DA.SetData(1, false);
                DA.SetData(2, "Invalid file path");
                return;
            }

            if (!File.Exists(filePath))
            {
                DA.SetData(1, false);
                DA.SetData(2, $"File not found: {filePath}");
                return;
            }

            // Import model from E2K
            try
            {
                // Import using E2KImporter
                var importer = new E2KImporter();
                BaseModel model = importer.ImportFromE2K(filePath);

                DA.SetData(0, model);
                DA.SetData(1, true);
                DA.SetData(2, $"Successfully imported model from {filePath}");
            }
            catch (Exception ex)
            {
                DA.SetData(1, false);
                DA.SetData(2, $"Import failed: {ex.Message}");
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
            get { return new Guid("B3FA8C45-D96E-4E82-891A-AC5ED43C8F9E"); }
        }
    }
}
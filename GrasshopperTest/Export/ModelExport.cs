using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Converters;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Model;
using Core.Models.Properties;


namespace Grasshopper.Export
{
    public class StructuralModelExport : GH_Component
    {
        private Base _base;

        /// <summary>
        /// Initializes a new instance of the StructuralModelExport class.
        /// </summary>
        public StructuralModelExport()
          : base("Structural Model Export", "StructExport",
              "Export a complete structural model to JSON",
              "Structural", "Export")
        {
            _base = new Base();
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Grids", "G", "Grids to include in the model", GH_ParamAccess.list);
            pManager.AddTextParameter("ProjectName", "P", "Project name for metadata", GH_ParamAccess.item);
            pManager.AddTextParameter("FilePath", "F", "Path to save the JSON file", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Export", "E", "Trigger export (set to true)", GH_ParamAccess.item);

            // Optional inputs can be added later for other structural elements
            //pManager.AddGenericParameter("Levels", "L", "Levels to include in the model", GH_ParamAccess.list, null);
            //pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Complete structural model", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if export was successful", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "I", "Result message", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Reset the building structure
            _base = new Base();

            // Retrieve input data
            List<Grid> grids = new List<Grid>();
            string projectName = string.Empty;
            string filePath = string.Empty;
            bool exportTrigger = false;
            List<Level> levels = new List<Level>();

            if (!DA.GetDataList(0, grids)) return;
            if (!DA.GetData(1, ref projectName)) return;
            if (!DA.GetData(2, ref filePath)) return;
            if (!DA.GetData(3, ref exportTrigger)) return;
            DA.GetDataList(4, levels); // Optional, so no validation needed

            // Basic validation
            if (string.IsNullOrWhiteSpace(projectName))
            {
                DA.SetData(1, false);
                DA.SetData(2, "Project name cannot be empty");
                return;
            }

            try
            {
                // Set metadata
                _base.Metadata.ProjectName = projectName;
                _base.Metadata.CreationDate = DateTime.Now;
                _base.Metadata.SchemaVersion = "1.0";

                // Set units (you can make these configurable later)
                _base.Units.Length = "inches";
                _base.Units.Force = "pounds";
                _base.Units.Temperature = "fahrenheit";

                // Add grids
                _base.Model.Grids = grids;

                // Add levels
                if (levels != null && levels.Count > 0)
                {
                    _base.Model.Levels = levels;
                }

                // Validate
                //ValidationResult validationResult = YourSolution.Core.Validation.BuildingStructureValidator.Validate(_base);
                //if (!validationResult.IsValid)
                //{
                //    DA.SetData(1, false);
                //    DA.SetData(2, $"Validation failed: {string.Join(", ", validationResult.Errors)}");
                //    return;
                //}

                // Export if trigger is true
                if (exportTrigger)
                {
                    JsonConverter.SaveToFile(_base, filePath);
                    DA.SetData(1, true);
                    DA.SetData(2, $"Successfully exported model with {grids.Count} grids and {levels.Count} levels to {filePath}");
                }
                else
                {
                    DA.SetData(1, true);
                    DA.SetData(2, "Model ready for export. Set Export to True to save the file.");
                }

                // Set model output
                DA.SetData(0, _base);
            }
            catch (Exception ex)
            {
                DA.SetData(1, false);
                DA.SetData(2, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon
        {
            get
            {
                // You can add custom icon here
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("d7e8f9a0-b1c2-4d3e-5f6g-7h8i9j0k1l2m"); }
        }
    }
}
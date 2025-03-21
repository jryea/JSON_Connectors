using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Loads;
using Core.Models.Metadata;

namespace Grasshopper.Export
{
    public class LayoutContainerComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the LayoutContainer class.
        /// </summary>
        public LayoutContainerComponent()
          : base("Model Layout", "Layout",
              "Creates a model layout container with grids, levels, and floor types",
              "IMEG", "Model Layout")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Levels", "L", "Level definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Grids", "G", "Grid definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Floor Types", "FT", "Floor type definitions (for RAM)", GH_ParamAccess.list);

            // Set optional parameters
            pManager[2].Optional = true;
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Layout", "L", "Model layout container", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<Grid> grids = new List<Grid>();
            List<Level> levels = new List<Level>();
            List<FloorType> floorTypes = new List<FloorType>();
            List<string> designCodes = new List<string>();

            // Get data - all inputs are optional
            DA.GetDataList(0, grids);
            DA.GetDataList(1, levels);
            DA.GetDataList(2, floorTypes);

            try
            {
                // Create layout container
                ModelLayoutContainer modelLayout = new ModelLayoutContainer();

                // Add components
                modelLayout.Grids = grids;
                modelLayout.Levels = levels;
                modelLayout.FloorTypes = floorTypes;

                // Generate summary for feedback
                string summary = $"Layout contains: {grids.Count} grids, {levels.Count} levels, " +
                                 $"{floorTypes.Count} floor types, {designCodes.Count} design codes";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, summary);

                // Set output
                DA.SetData(0, modelLayout);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("7F8E9D0C-1B2A-3C4D-5E6F-7A8B9C0D1E2F");
    }
}
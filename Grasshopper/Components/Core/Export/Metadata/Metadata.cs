using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Metadata;

namespace Grasshopper.Components.Core.Export.Metadata
{
    public class MetadataComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Metadata component.
        /// </summary>
        public MetadataComponent()
          : base("Metadata", "Meta",
              "Creates a complete metadata package for the structural model",
              "IMEG", "Metadata")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Option 1: Use pre-created components
            pManager.AddGenericParameter("Project Info", "PI", "Project information (optional)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Units", "U", "Units definition (optional)", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Metadata", "PI", "Metadata for the structural model", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        /// s
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data - pre-created components
            ProjectInfo inputProjectInfo = null;
            Units inputUnits = null;
            MetadataContainer metadata = null;

            // Try to get pre-created components
            bool hasProjectInfo = DA.GetData(0, ref inputProjectInfo);
            bool hasUnits = DA.GetData(1, ref inputUnits);


            try
            {
                metadata = new MetadataContainer();
                metadata.Units = inputUnits;
                metadata.ProjectInfo = inputProjectInfo;

                // Set outputs
                DA.SetData(0, metadata);
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
        public override Guid ComponentGuid => new Guid("E5F6A7B8-C9D0-1E2F-3A4B-5C6D7E8F9A0B");
    }
}
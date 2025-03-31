using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Metadata;
using Grasshopper.Components.Core;

namespace Grasshopper.Components.Core.Export.Metadata
{
    public class MetadataComponent : ComponentBase
    {
        // Initializes a new instance of the Metadata component.
        public MetadataComponent()
          : base("Metadata", "Meta",
              "Creates a complete metadata package for the structural model",
              "IMEG", "Metadata")
        {
        }

        // Registers all the input parameters for this component.
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Option 1: Use pre-created components
            pManager.AddGenericParameter("Project Info", "PI", "Project information (optional)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Units", "U", "Units definition (optional)", GH_ParamAccess.item);
        }

        // Registers all the output parameters for this component.
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Metadata", "PI", "Metadata for the structural model", GH_ParamAccess.item);
        }

        // This is the method that actually does the work.
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

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid => new Guid("E5F6A7B8-C9D0-1E2F-3A4B-5C6D7E8F9A0B");
    }
}
using System;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Metadata;
using System.Collections.Generic;

namespace Grasshopper.Components.Core.Export.Metadata
{
    public class ProjectInfoComponent : ComponentBase
    {
        // Initializes a new instance of the ProjectInfo component.
        public ProjectInfoComponent()
          : base("Project Info", "ProjInfo",
              "Creates project information for the structural model",
              "IMEG", "Metadata")
        {
        }

        // Registers all the input parameters for this component.
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project Name", "N", "Name of the project", GH_ParamAccess.item, "New Project");
        }

        // Registers all the output parameters for this component.
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Project Info", "PI", "Project information for the structural model", GH_ParamAccess.item);
        }

        // This is the method that actually does the work.
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            string projectName = "New Project";
            string projectId = string.Empty;
            string schemaVersion = "1.0";

            DA.GetData(0, ref projectName);

            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(projectName))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Project name is empty, using default 'New Project'");
                    projectName = "New Project";
                }

                // Create ProjectInfo object
                ProjectInfo projectInfo = new ProjectInfo
                {
                    ProjectName = projectName,
                    SchemaVersion = schemaVersion,
                    CreationDate = DateTime.Now
                };

                // Set output
                DA.SetData(0, projectInfo);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid => new Guid("C4D5E6F7-A8B9-0C1D-2E3F-4A5B6C7D8E9F");
    }
}
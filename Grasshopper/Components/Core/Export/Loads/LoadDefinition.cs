using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using CM = Core.Models.Loads;
using Grasshopper.Components.Core;
using Grasshopper.Utilities;
using System.Drawing;

namespace Grasshopper.Components
{
    public class LoadDefinitionComponent : ComponentBase
    {
        // Initializes a new instance of the LoadDefinitionComponent class.
        public LoadDefinitionComponent()
          : base("Load Definition", "LoadDefinition",
              "Creates a load definition",
              "IMEG", "Loads")
        {
        }

        // Registers all the input parameters for this component.
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name of the load definition", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Type of load pattern (Dead, Live, Wind, etc.)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Self Weight", "SW", "Self weight multiplier (typically 1 for Dead load, 0 for others)", GH_ParamAccess.item, 0);
            pManager[2].Optional = true;
        }

        // Registers all the output parameters for this component.
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Load Definition", "LD", "Load definition", GH_ParamAccess.item);
        }

        // This is the method that actually does the work.
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize variables
            string name = string.Empty;
            string type = string.Empty;
            double selfWeight = 0;

            // Retrieve input data
            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetData(1, ref type)) return;
            DA.GetData(2, ref selfWeight);

            // Create the load definition
            CM.LoadDefinition loadDef = new CM.LoadDefinition
            {
                Name = name,
                Type = type,
                SelfWeight = selfWeight
            };

            // Output the load definition wrapped in a Goo object
            DA.SetData(0, new GH_LoadDefinition(loadDef));
        }

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid
        {
            get { return new Guid("73AD3CEC-4CDE-4689-A49E-9C0B1A26F4E5"); }
        }
    }
}

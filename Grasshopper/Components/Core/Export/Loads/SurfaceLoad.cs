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
    public class SurfaceLoadComponent : ComponentBase
    {
        // Initializes a new instance of the SurfaceLoadComponent class.
        public SurfaceLoadComponent()
          : base("Surface Load", "SurfaceLoad",
              "Creates a surface load",
              "IMEG", "Loads")
        {
        }

        // Registers all the input parameters for this component.
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Floor Type", "FT", "Floor type object", GH_ParamAccess.item);
            pManager.AddGenericParameter("Live Load", "LL", "Live load definition object", GH_ParamAccess.item);
            pManager.AddGenericParameter("Dead Load", "DL", "Dead load definition object", GH_ParamAccess.item);
        }

        // Registers all the output parameters for this component.
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Surface Load", "SL", "Surface load", GH_ParamAccess.item);
        }

        // This is the method that actually does the work.
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize variables
            GH_FloorType ghFloorType = null;
            GH_LoadDefinition ghLiveLoad = null;
            GH_LoadDefinition ghDeadLoad = null;

            // Retrieve input data
            if (!DA.GetData(0, ref ghFloorType)) return;
            if (!DA.GetData(1, ref ghLiveLoad)) return;
            if (!DA.GetData(2, ref ghDeadLoad)) return;

            // Create the surface load
            CM.SurfaceLoad surfaceLoad = new CM.SurfaceLoad
            {
                LayoutTypeId = ghFloorType.Value.Id,
                LiveLoadId = ghLiveLoad.Value.Id,
                DeadLoadId = ghDeadLoad.Value.Id
            };

            // Output the surface load wrapped in a Goo object
            DA.SetData(0, new GH_SurfaceLoad(surfaceLoad));
        }

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid
        {
            get { return new Guid("B1C2D3E4-F5A6-7890-1234-56789ABCDEF1"); }
        }
    }
}

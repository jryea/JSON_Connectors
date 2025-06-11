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
        public SurfaceLoadComponent()
          : base("Surface Load", "SurfaceLoad",
              "Creates a surface load",
              "IMEG", "Loads")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name of the surface load", GH_ParamAccess.item, "Surface Load");
            pManager.AddGenericParameter("Floor Type", "FT", "Floor type object", GH_ParamAccess.item);
            pManager.AddGenericParameter("Live Load", "LL", "Live load definition object", GH_ParamAccess.item);
            pManager.AddGenericParameter("Dead Load", "DL", "Dead load definition object", GH_ParamAccess.item);
            pManager.AddNumberParameter("Live Load Value", "LLV", "Live load value (psf)", GH_ParamAccess.item, 50.0);
            pManager.AddNumberParameter("Dead Load Value", "DLV", "Dead load value (psf)", GH_ParamAccess.item, 15.0);

            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Surface Load", "SL", "Surface load", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize variables
            string name = "Surface Load"; // Default name
            GH_FloorType ghFloorType = null;
            GH_LoadDefinition ghLiveLoad = null;
            GH_LoadDefinition ghDeadLoad = null;
            double liveLoadValue = 50.0; // Default value
            double deadLoadValue = 15.0; // Default value

            // Retrieve input data
            DA.GetData(0, ref name);
            if (!DA.GetData(1, ref ghFloorType)) return;
            if (!DA.GetData(2, ref ghLiveLoad)) return;
            if (!DA.GetData(3, ref ghDeadLoad)) return;
            DA.GetData(4, ref liveLoadValue);
            DA.GetData(5, ref deadLoadValue);

            // Create the surface load
            CM.SurfaceLoad surfaceLoad = new CM.SurfaceLoad
            {
                Name = name,    
                LayoutTypeId = ghFloorType.Value.Id,
                LiveLoadId = ghLiveLoad.Value.Id,
                DeadLoadId = ghDeadLoad.Value.Id,
                LiveLoadValue = liveLoadValue,
                DeadLoadValue = deadLoadValue
            };

            // Output the surface load wrapped in a Goo object
            DA.SetData(0, new GH_SurfaceLoad(surfaceLoad));
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("B1C2D3E4-F5A6-7890-1234-56789ABCDEF1"); }
        }
    }
}

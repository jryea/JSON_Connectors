using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using Core.Models.ModelLayout;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.ModelLayout
{
    public class FloorTypeCollectorComponent : ComponentBase
    {
        public FloorTypeCollectorComponent()
          : base("Floor Types", "FloorTypes",
              "Creates floor type objects for the structural model",
              "IMEG", "Model Layout")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each floor type", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("FloorTypes", "FT", "Floor type objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> names = new List<string>();
            if (!DA.GetDataList(0, names)) return;

            List<GH_FloorType> floorTypes = new List<GH_FloorType>();
            foreach (string name in names)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    FloorType floorType = new FloorType(name);
                    floorTypes.Add(new GH_FloorType(floorType));
                }
            }

            DA.SetDataList(0, floorTypes);
        }

        public override Guid ComponentGuid => new Guid("1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D");
    }
}
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.Geometry;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class IsolatedFootingCollectorComponent : ComponentBase
    {
        public IsolatedFootingCollectorComponent()
          : base("Isolated Footings", "Footings",
              "Creates isolated footing objects that can be used in the structural model",
              "IMEG", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Points representing footing locations", GH_ParamAccess.list);
            pManager.AddNumberParameter("Z Coordinate", "Z", "Z coordinate of the footings (optional)", GH_ParamAccess.item, 0.0);

            // Make Z parameter optional
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Isolated Footings", "F", "Isolated footing objects for the structural model", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> points = new List<Point3d>();
            double zCoordinate = 0.0;

            if (!DA.GetDataList(0, points)) return;
            DA.GetData(1, ref zCoordinate); // Optional, default is 0.0

            // Basic validation
            if (points.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No footing points provided");
                return;
            }

            List<GH_IsolatedFooting> footings = new List<GH_IsolatedFooting>();
            foreach (var point in points)
            {
                // Create a new isolated footing
                IsolatedFooting footing = new IsolatedFooting
                {
                    Point = new Point3D(point.X * 12, point.Y * 12, zCoordinate * 12)
                };

                footings.Add(new GH_IsolatedFooting(footing));
            }

            DA.SetDataList(0, footings);
        }

        public override Guid ComponentGuid => new Guid("3F4A5B6C-7D8E-9F0A-1B2C-3D4E5F6A7B8C");
    }
}
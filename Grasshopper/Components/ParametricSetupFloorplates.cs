using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Grasshopper.Components.Core;

namespace Grasshopper.Components
{

    public class FloorplateElevationsComponent : ComponentBase
    {
        public FloorplateElevationsComponent()
            : base("Floorplate Elevations", "FloorEl",
                "Extract elevation data from floorplate geometry",
                "IMEG", "Structural")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Setup Geometry", "G", "Geometry pipeline from z-setup layer", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Elevations", "E", "Elevations for each floorplate", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get input geometry tree
            GH_Structure<IGH_GeometricGoo> geometry;
            if (!DA.GetDataTree(0, out geometry)) return;

            // Create output tree
            GH_Structure<GH_Number> elevations = new GH_Structure<GH_Number>();

            // Process each branch (which should correspond to a floorplate)
            for (int i = 0; i < geometry.Branches.Count; i++)
            {
                var branch = geometry.Branches[i];
                var path = geometry.Paths[i];
                List<double> branchElevations = new List<double>();

                // Extract Z values from all curves in this branch
                foreach (var goo in branch)
                {
                    if (goo is GH_Curve ghCurve)
                    {
                        Curve curve = ghCurve.Value;
                        double z = curve.PointAtStart.Z;

                        // Only add unique elevations
                        if (!branchElevations.Contains(z))
                            branchElevations.Add(z);
                    }
                }

                // Convert to GH_Number and add to output tree
                var ghNumbers = branchElevations.ConvertAll(z => new GH_Number(z));
                elevations.AppendRange(ghNumbers, path);
            }

            DA.SetDataTree(0, elevations);
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("c4d5e6f7-a8b9-c0d1-e2f3-456789abcdef"); }
        }
    }
}
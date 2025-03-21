using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Core.Models.Elements;
using Core.Models.Model;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class IsolatedFootingCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the IsolatedFootingCollector class.
        /// </summary>
        public IsolatedFootingCollectorComponent()
          : base("Isolated Footings", "Footings",
              "Creates isolated footing objects that can be used in the structural model",
              "IMEG", "Elements")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Points representing footing locations", GH_ParamAccess.list);
            pManager.AddNumberParameter("Z Coordinate", "Z", "Z coordinate of the footings (optional)", GH_ParamAccess.item, 0.0);

            // Make Z parameter optional
            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Isolated Footings", "F", "Isolated footing objects for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
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

            try
            {
                // Create isolated footings
                List<IsolatedFooting> footings = new List<IsolatedFooting>();

                foreach (var point in points)
                {
                    // Create a new isolated footing
                    IsolatedFooting footing = new IsolatedFooting();

                    // Set the point (converting to inches if Rhino is in feet)
                    footing.Point = new Point3D(
                        point.X * 12,
                        point.Y * 12,
                        zCoordinate * 12);

                    footings.Add(footing);
                }

                // Set output
                DA.SetDataList(0, footings);
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
        public override Guid ComponentGuid => new Guid("3F4A5B6C-7D8E-9F0A-1B2C-3D4E5F6A7B8C");
    }
}
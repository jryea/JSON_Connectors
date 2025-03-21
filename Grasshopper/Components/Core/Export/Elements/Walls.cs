using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Core.Models.Elements;
using Core.Models.ModelLayout;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class WallCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WallCollector class.
        /// </summary>
        public WallCollectorComponent()
          : base("Walls", "Walls",
              "Creates wall objects that can be used in the structural model",
              "IMEG", "Elements")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddSurfaceParameter("Surfaces", "S", "Surfaces representing walls", GH_ParamAccess.list);
            pManager.AddTextParameter("Properties ID", "P", "ID of the wall properties", GH_ParamAccess.list);
            pManager.AddTextParameter("Pier/Spandrel ID", "PS", "ID of the pier/spandrel configuration (optional)", GH_ParamAccess.list);

            // Make some parameters optional
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Walls", "W", "Wall objects for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<Surface> surfaces = new List<Surface>();
            List<string> propertiesIds = new List<string>();
            List<string> pierSpandrelIds = new List<string>();

            if (!DA.GetDataList(0, surfaces)) return;
            if (!DA.GetDataList(1, propertiesIds)) return;
            DA.GetDataList(2, pierSpandrelIds); // Optional

            // Basic validation
            if (surfaces.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No wall surfaces provided");
                return;
            }

            if (surfaces.Count != propertiesIds.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of surfaces ({surfaces.Count}) must match number of properties IDs ({propertiesIds.Count})");
                return;
            }

            // Ensure optional lists have the right size or are empty
            if (pierSpandrelIds.Count > 0 && pierSpandrelIds.Count != surfaces.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"If provided, the number of pier/spandrel IDs ({pierSpandrelIds.Count}) must match number of surfaces ({surfaces.Count})");
                return;
            }

            try
            {
                // Create walls
                List<Wall> walls = new List<Wall>();

                for (int i = 0; i < surfaces.Count; i++)
                {
                    Surface surface = surfaces[i];

                    // Create a new wall
                    Wall wall = new Wall();

                    // Extract points from the surface
                    // For walls, we typically want the plan view outline (projection onto XY plane)
                    var points = new List<Point2D>();

                    // Get a reasonable approximation of the wall outline
                    // This is a simplification - in production code, you'd extract actual boundary curves
                    NurbsSurface nurbsSurface = surface.ToNurbsSurface();

                    // Get the boundary
                    //Curve[] boundaries = nurbsSurface.GetBoundingCurves(true);
                    //if (boundaries != null && boundaries.Length > 0)
                    //{
                    //    // Flatten to XY plane for plan view
                    //    Curve planCurve = boundaries[0].ProjectToPlane(Plane.WorldXY);

                    //    // Sample points along the curve
                    //    double length = planCurve.GetLength();
                    //    int pointCount = Math.Max(4, (int)(length / 2.0));

                    //    for (int j = 0; j < pointCount; j++)
                    //    {
                    //        double param = (double)j / (pointCount - 1);
                    //        Point3d pt = planCurve.PointAt(param);

                    //        // Convert to inches (assuming Rhino is in feet)
                    //        points.Add(new Point2D(pt.X * 12, pt.Y * 12));
                    //    }
                    //}

                    // Set the wall properties
                    wall.PropertiesId = propertiesIds[i];
                    wall.Points = points;

                    // Set optional properties if provided
                    if (pierSpandrelIds.Count > 0)
                    {
                        wall.PierSpandrelId = pierSpandrelIds[i];
                    }

                    walls.Add(wall);
                }

                // Set output
                DA.SetDataList(0, walls);
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
        public override Guid ComponentGuid => new Guid("B45C3A75-A162-4F76-8E3A-89F2D8D3C0EC");
    }
}
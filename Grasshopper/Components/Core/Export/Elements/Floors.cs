using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Core.Models.Elements;
using Core.Models.Model;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class FloorCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FloorCollector class.
        /// </summary>
        public FloorCollectorComponent()
          : base("Floors", "Floors",
              "Creates floor objects that can be used in the structural model",
              "IMEG", "Elements")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddSurfaceParameter("Surfaces", "S", "Surfaces representing floors", GH_ParamAccess.list);
            pManager.AddTextParameter("Level ID", "L", "ID of the level this floor belongs to", GH_ParamAccess.list);
            pManager.AddTextParameter("Properties ID", "P", "ID of the floor properties", GH_ParamAccess.list);
            pManager.AddTextParameter("Diaphragm ID", "D", "ID of the diaphragm (optional)", GH_ParamAccess.list);
            pManager.AddTextParameter("Surface Load ID", "SL", "ID of the surface load (optional)", GH_ParamAccess.list);

            // Make some parameters optional
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Floors", "F", "Floor objects for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<Surface> surfaces = new List<Surface>();
            List<string> levelIds = new List<string>();
            List<string> propertiesIds = new List<string>();
            List<string> diaphragmIds = new List<string>();
            List<string> surfaceLoadIds = new List<string>();

            if (!DA.GetDataList(0, surfaces)) return;
            if (!DA.GetDataList(1, levelIds)) return;
            if (!DA.GetDataList(2, propertiesIds)) return;
            DA.GetDataList(3, diaphragmIds); // Optional
            DA.GetDataList(4, surfaceLoadIds); // Optional

            // Basic validation
            if (surfaces.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No floor surfaces provided");
                return;
            }

            if (surfaces.Count != levelIds.Count || surfaces.Count != propertiesIds.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of surfaces ({surfaces.Count}) must match number of level IDs ({levelIds.Count}) and properties IDs ({propertiesIds.Count})");
                return;
            }

            // Ensure optional lists have the right size or are empty
            if (diaphragmIds.Count > 0 && diaphragmIds.Count != surfaces.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"If provided, the number of diaphragm IDs ({diaphragmIds.Count}) must match number of surfaces ({surfaces.Count})");
                return;
            }

            if (surfaceLoadIds.Count > 0 && surfaceLoadIds.Count != surfaces.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"If provided, the number of surface load IDs ({surfaceLoadIds.Count}) must match number of surfaces ({surfaces.Count})");
                return;
            }

            try
            {
                // Create floors
                List<Floor> floors = new List<Floor>();

                for (int i = 0; i < surfaces.Count; i++)
                {
                    Surface surface = surfaces[i];

                    // Create a new floor
                    Floor floor = new Floor();

                    // Extract points from the surface
                    var points = new List<Point3D>();

                    // Get control points from the surface
                    // This is a simplification - in a real implementation,
                    // you'd want to extract the actual boundary points of the surface
                    NurbsSurface nurbsSurface = surface.ToNurbsSurface();
                    for (int u = 0; u < nurbsSurface.Points.CountU; u++)
                    {
                        for (int v = 0; v < nurbsSurface.Points.CountV; v++)
                        {
                            var controlPoint = nurbsSurface.Points.GetControlPoint(u, v);
                            // Convert to inches (assuming Rhino is in feet)
                            points.Add(new Point3D(
                                controlPoint.Location.X * 12,
                                controlPoint.Location.Y * 12,
                                controlPoint.Location.Z * 12
                            ));
                        }
                    }

                    // Set the floor properties
                    floor.LevelId = levelIds[i];
                    floor.PropertiesId = propertiesIds[i];
                    floor.Points = points;

                    // Set optional properties if provided
                    if (diaphragmIds.Count > 0)
                    {
                        floor.DiaphragmId = diaphragmIds[i];
                    }

                    if (surfaceLoadIds.Count > 0)
                    {
                        floor.SurfaceLoadId = surfaceLoadIds[i];
                    }

                    floors.Add(floor);
                }

                // Set output
                DA.SetDataList(0, floors);
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
        public override Guid ComponentGuid => new Guid("8D3932A5-1B74-43C6-9BC7-8C0C21B932C5");
    }
}
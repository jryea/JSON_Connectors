using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Grasshopper.Utilities;

namespace JSON_Connectors.Components.Core.Export.Elements
{
    public class FloorCollectorComponent : GH_Component
    {
        public FloorCollectorComponent()
          : base("Floors", "Floors",
              "Creates floor objects for the structural model",
              "IMEG", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddSurfaceParameter("Surfaces", "S", "Surfaces representing floors", GH_ParamAccess.list);
            pManager.AddTextParameter("Level ID", "L", "ID of the level", GH_ParamAccess.list);
            pManager.AddTextParameter("Properties ID", "P", "ID of the floor properties", GH_ParamAccess.list);
            pManager.AddTextParameter("Diaphragm ID", "D", "ID of the diaphragm (optional)", GH_ParamAccess.list);
            pManager.AddTextParameter("Surface Load ID", "SL", "ID of the surface load (optional)", GH_ParamAccess.list);

            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Floors", "F", "Floor objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Surface> surfaces = new List<Surface>();
            List<string> levelIds = new List<string>();
            List<string> propertiesIds = new List<string>();
            List<string> diaphragmIds = new List<string>();
            List<string> surfaceLoadIds = new List<string>();

            if (!DA.GetDataList(0, surfaces)) return;
            if (!DA.GetDataList(1, levelIds)) return;
            if (!DA.GetDataList(2, propertiesIds)) return;
            DA.GetDataList(3, diaphragmIds);
            DA.GetDataList(4, surfaceLoadIds);

            if (surfaces.Count != levelIds.Count || surfaces.Count != propertiesIds.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of surfaces must match number of level IDs and properties IDs");
                return;
            }

            List<GH_Floor> floors = new List<GH_Floor>();
            for (int i = 0; i < surfaces.Count; i++)
            {
                Surface srf = surfaces[i];
                NurbsSurface nurbsSrf = srf.ToNurbsSurface();

                // Extract points from the surface
                List<Point3D> points = new List<Point3D>();
                for (int u = 0; u <= 1; u++)
                {
                    for (int v = 0; v <= 1; v++)
                    {
                        Point3d pt = nurbsSrf.PointAt(u, v);
                        points.Add(new Point3D(pt.X * 12, pt.Y * 12, pt.Z * 12));
                    }
                }

                Floor floor = new Floor
                {
                    LevelId = levelIds[i],
                    PropertiesId = propertiesIds[i],
                    Points = points,
                    DiaphragmId = diaphragmIds.Count > i ? diaphragmIds[i] : null,
                    SurfaceLoadId = surfaceLoadIds.Count > i ? surfaceLoadIds[i] : null
                };

                floors.Add(new GH_Floor(floor));
            }

            DA.SetDataList(0, floors);
        }

        public override Guid ComponentGuid => new Guid("8D3932A5-1B74-43C6-9BC7-8C0C21B932C5");
    }
}
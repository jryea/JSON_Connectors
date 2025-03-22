using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Grasshopper.Utilities;

namespace JSON_Connectors.Components.Core.Export.Elements
{
    public class WallCollectorComponent : GH_Component
    {
        public WallCollectorComponent()
          : base("Walls", "Walls",
              "Creates wall objects for the structural model",
              "IMEG", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Curves representing wall centerlines", GH_ParamAccess.list);
            pManager.AddTextParameter("Properties ID", "P", "ID of the wall properties", GH_ParamAccess.list);
            pManager.AddTextParameter("Pier/Spandrel ID", "PS", "ID of the pier/spandrel config (optional)", GH_ParamAccess.list);

            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Walls", "W", "Wall objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> curves = new List<Curve>();
            List<string> propertiesIds = new List<string>();
            List<string> pierSpandrelIds = new List<string>();

            if (!DA.GetDataList(0, curves)) return;
            if (!DA.GetDataList(1, propertiesIds)) return;
            DA.GetDataList(2, pierSpandrelIds);

            if (curves.Count != propertiesIds.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of curves must match number of properties IDs");
                return;
            }

            if (pierSpandrelIds.Count > 0 && pierSpandrelIds.Count != curves.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "If provided, number of pier/spandrel IDs must match number of curves");
                return;
            }

            List<GH_Wall> walls = new List<GH_Wall>();
            for (int i = 0; i < curves.Count; i++)
            {
                // Sample points along the curve to create wall points
                Curve curve = curves[i];
                List<Point2D> points = new List<Point2D>();

                // Simple sampling of points along the curve
                int numPoints = Math.Max(2, (int)(curve.GetLength() / 12.0));
                for (int j = 0; j < numPoints; j++)
                {
                    double t = (double)j / (numPoints - 1);
                    Point3d point = curve.PointAt(t);
                    points.Add(new Point2D(point.X * 12, point.Y * 12));
                }

                Wall wall = new Wall
                {
                    Points = points,
                    PropertiesId = propertiesIds[i],
                    PierSpandrelId = pierSpandrelIds.Count > i ? pierSpandrelIds[i] : null
                };

                walls.Add(new GH_Wall(wall));
            }

            DA.SetDataList(0, walls);
        }

        public override Guid ComponentGuid => new Guid("B45C3A75-A162-4F76-8E3A-89F2D8D3C0EC");
    }
}
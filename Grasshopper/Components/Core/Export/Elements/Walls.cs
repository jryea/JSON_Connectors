using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.Properties;
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
            pManager.AddGenericParameter("Properties", "P", "Wall properties", GH_ParamAccess.list);
            pManager.AddGenericParameter("Pier/Spandrel", "PS", "Pier/spandrel configuration (optional)", GH_ParamAccess.list);

            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Walls", "W", "Wall objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> curves = new List<Curve>();
            List<object> propObjs = new List<object>();
            List<object> pierSpandrelObjs = new List<object>();

            if (!DA.GetDataList(0, curves)) return;
            if (!DA.GetDataList(1, propObjs)) return;
            DA.GetDataList(2, pierSpandrelObjs);

            if (curves.Count != propObjs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of curves must match number of properties");
                return;
            }

            if (pierSpandrelObjs.Count > 0 && pierSpandrelObjs.Count != curves.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "If provided, number of pier/spandrel objects must match number of curves");
                return;
            }

            List<GH_Wall> walls = new List<GH_Wall>();
            for (int i = 0; i < curves.Count; i++)
            {
                Curve curve = curves[i];
                WallProperties wallProps = ExtractObject<WallProperties>(propObjs[i], "WallProperties");
                object pierSpandrel = pierSpandrelObjs.Count > i ? pierSpandrelObjs[i] : null;

                if (wallProps == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid properties at index {i}");
                    continue;
                }

                // Sample points along the curve to create wall points
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
                    PropertiesId = wallProps.Name,
                    PierSpandrelId = pierSpandrel?.ToString()
                };

                walls.Add(new GH_Wall(wall));
            }

            DA.SetDataList(0, walls);
        }

        private T ExtractObject<T>(object obj, string typeName) where T : class
        {
            if (obj is T directType)
                return directType;

            if (obj is GH_ModelGoo<T> ghType)
                return ghType.Value;

            // Try to handle string IDs (for compatibility)
            if (obj is string && typeof(T) == typeof(WallProperties))
            {
                return new WallProperties { Name = (string)obj } as T;
            }

            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Could not extract {typeName}");
            return null;
        }

        public override Guid ComponentGuid => new Guid("B45C3A75-A162-4F76-8E3A-89F2D8D3C0EC");
    }
}
using Grasshopper.Kernel;
using RG = Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Geometry;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class IsolatedFootingCollectorComponent : ComponentBase
    {
        public IsolatedFootingCollectorComponent()
          : base("Isolated Footing", "Footings",
              "Creates isolated footing objects that can be used in the structural model",
              "IMEG", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Points representing footing locations", GH_ParamAccess.list);
            pManager.AddGenericParameter("Levels", "L", "Level for each footing", GH_ParamAccess.list);
            pManager.AddNumberParameter("Width", "W", "Width of each footing (in inches)", GH_ParamAccess.list, 48.0);
            pManager.AddNumberParameter("Depth", "D", "Depth of each footing (in inches)", GH_ParamAccess.list, 48.0);
            pManager.AddNumberParameter("Thickness", "T", "Thickness of each footing (in inches)", GH_ParamAccess.list, 24.0);

            // Make all parameters except Points optional
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Isolated Footings", "F", "Isolated footing objects for the structural model", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<RG.Point3d> points = new List<RG.Point3d>();
            List<object> levelObjs = new List<object>();
            List<double> widths = new List<double>();
            List<double> depths = new List<double>();
            List<double> thicknesses = new List<double>();

            if (!DA.GetDataList(0, points)) return;
            DA.GetDataList(1, levelObjs);
            DA.GetDataList(2, widths);
            DA.GetDataList(3, depths);
            DA.GetDataList(4, thicknesses);

            // Basic validation
            if (points.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No footing points provided");
                return;
            }

            // Extend level objects list to match points count
            if (levelObjs.Count > 0 && levelObjs.Count < points.Count)
            {
                object lastLevel = levelObjs[levelObjs.Count - 1];
                while (levelObjs.Count < points.Count)
                    levelObjs.Add(lastLevel);
            }

            // Extend widths list to match points count
            if (widths.Count > 0 && widths.Count < points.Count)
            {
                double lastWidth = widths[widths.Count - 1];
                while (widths.Count < points.Count)
                    widths.Add(lastWidth);
            }
            else if (widths.Count == 0)
            {
                // Default width (48 inches)
                widths = Enumerable.Repeat(48.0, points.Count).ToList();
            }

            // Extend depths list to match points count
            if (depths.Count > 0 && depths.Count < points.Count)
            {
                double lastDepth = depths[depths.Count - 1];
                while (depths.Count < points.Count)
                    depths.Add(lastDepth);
            }
            else if (depths.Count == 0)
            {
                // Default depth (48 inches)
                depths = Enumerable.Repeat(48.0, points.Count).ToList();
            }

            // Extend thicknesses list to match points count
            if (thicknesses.Count > 0 && thicknesses.Count < points.Count)
            {
                double lastThickness = thicknesses[thicknesses.Count - 1];
                while (thicknesses.Count < points.Count)
                    thicknesses.Add(lastThickness);
            }
            else if (thicknesses.Count == 0)
            {
                // Default thickness (24 inches)
                thicknesses = Enumerable.Repeat(24.0, points.Count).ToList();
            }

            List<GH_IsolatedFooting> footings = new List<GH_IsolatedFooting>();

            for (int i = 0; i < points.Count; i++)
            {
                // Extract level if available
                Level level = null;
                if (levelObjs.Count > i)
                {
                    level = ExtractObject<Level>(levelObjs[i], "Level");
                }

                // Create a new isolated footing
                IsolatedFooting footing = new IsolatedFooting()
                {
                    Point = new Point3D(points[i].X * 12, points[i].Y * 12, points[i].Z * 12),
                    LevelId = level.Id,
                    Width = widths[i],
                    Length = depths[i],
                    Thickness = thicknesses[i]
                };
                

                footings.Add(new GH_IsolatedFooting(footing));
            }

            DA.SetDataList(0, footings);
        }

        private T ExtractObject<T>(object obj, string typeName) where T : class
        {
            // Direct type check
            if (obj is T directType)
                return directType;

            // Using GooWrapper
            if (obj is GH_ModelGoo<T> ghType)
                return ghType.Value;

            // Try to handle string IDs (for compatibility)
            if (obj is string && typeof(T) == typeof(Level))
            {
                return new Level((string)obj, null, 0) as T;
            }

            // If we got here, log the type we received for debugging
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                $"Could not extract {typeName} from input of type {obj?.GetType().Name ?? "null"}");

            return null;
        }

        public override Guid ComponentGuid => new Guid("3F4A5B6C-7D8E-9F0A-1B2C-3D4E5F6A7B8C");
    }
}
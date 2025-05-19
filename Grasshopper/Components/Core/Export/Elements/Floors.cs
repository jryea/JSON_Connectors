using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Loads;
using Grasshopper.Utilities;
using Grasshopper.Kernel.Types;
using Core.Models.Geometry;
using static Core.Models.Properties.Modifiers;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class FloorCollectorComponent : ComponentBase
    {
        public FloorCollectorComponent()
          : base("Floors", "Floors",
              "Creates floor objects for the structural model",
              "IMEG", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Points defining floor boundary", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Level", "L", "Level the floor belongs to", GH_ParamAccess.list);
            pManager.AddGenericParameter("Properties", "P", "Floor properties", GH_ParamAccess.list);
            pManager.AddGenericParameter("Diaphragm", "D", "Diaphragm (optional)", GH_ParamAccess.list);
            pManager.AddGenericParameter("Surface Load", "SL", "Surface load (optional)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Span Direction", "SD", "Span direction in degrees (0 = along X axis)", GH_ParamAccess.list, 0.0);
            pManager.AddGenericParameter("ETABS Modifiers", "EM", "ETABS-specific shell modifiers", GH_ParamAccess.list);

            pManager[3].Optional = true; // Diaphragm
            pManager[4].Optional = true; // Surface Load
            pManager[5].Optional = true; // Span Direction
            pManager[6].Optional = true; // ETABS Modifiers
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Floors", "F", "Floor objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Kernel.Data.GH_Structure<GH_Point> pointsTree;
            List<object> levelObjs = new List<object>();
            List<object> propObjs = new List<object>();
            List<object> diaphragmObjs = new List<object>();
            List<object> surfaceLoadObjs = new List<object>();
            List<double> spanDirections = new List<double>();
            List<object> etabsModObjs = new List<object>();

            if (!DA.GetDataTree(0, out pointsTree)) return;
            if (!DA.GetDataList(1, levelObjs)) return;
            if (!DA.GetDataList(2, propObjs)) return;
            DA.GetDataList(3, diaphragmObjs);
            DA.GetDataList(4, surfaceLoadObjs);
            DA.GetDataList(5, spanDirections);
            DA.GetDataList(6, etabsModObjs);

            // Extend level objects list if needed
            if (levelObjs.Count > 0 && levelObjs.Count < pointsTree.PathCount)
            {
                object lastLevel = levelObjs[levelObjs.Count - 1];
                while (levelObjs.Count < pointsTree.PathCount)
                    levelObjs.Add(lastLevel);
            }

            // Extend property objects list if needed
            if (propObjs.Count > 0 && propObjs.Count < pointsTree.PathCount)
            {
                object lastProp = propObjs[propObjs.Count - 1];
                while (propObjs.Count < pointsTree.PathCount)
                    propObjs.Add(lastProp);
            }

            // Extend diaphragm objects list if needed
            if (diaphragmObjs.Count > 0 && diaphragmObjs.Count < pointsTree.PathCount)
            {
                object lastDiaphragm = diaphragmObjs[diaphragmObjs.Count - 1];
                while (diaphragmObjs.Count < pointsTree.PathCount)
                    diaphragmObjs.Add(lastDiaphragm);
            }

            // Extend surface load objects list if needed
            if (surfaceLoadObjs.Count > 0 && surfaceLoadObjs.Count < pointsTree.PathCount)
            {
                object lastSurfaceLoad = surfaceLoadObjs[surfaceLoadObjs.Count - 1];
                while (surfaceLoadObjs.Count < pointsTree.PathCount)
                    surfaceLoadObjs.Add(lastSurfaceLoad);
            }

            // Extend span directions list if needed
            if (spanDirections.Count > 0 && spanDirections.Count < pointsTree.PathCount)
            {
                double lastDirection = spanDirections[spanDirections.Count - 1];
                while (spanDirections.Count < pointsTree.PathCount)
                    spanDirections.Add(lastDirection);
            }
            else if (spanDirections.Count == 0)
            {
                // Default to 0 degrees for all floors
                spanDirections = new List<double>(new double[pointsTree.PathCount]);
            }

            // Extend ETABS modifiers list if needed
            if (etabsModObjs.Count > 0 && etabsModObjs.Count < pointsTree.PathCount)
            {
                object lastModifier = etabsModObjs[etabsModObjs.Count - 1];
                while (etabsModObjs.Count < pointsTree.PathCount)
                    etabsModObjs.Add(lastModifier);
            }

            // Validate list lengths after extension
            if (pointsTree.PathCount != levelObjs.Count || pointsTree.PathCount != propObjs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of point sets must match number of levels and properties");
                return;
            }

            List<GH_Floor> floors = new List<GH_Floor>();

            for (int i = 0; i < pointsTree.PathCount; i++)
            {
                var path = pointsTree.Paths[i];
                var pointsBranch = pointsTree.get_Branch(path);

                if (pointsBranch.Count < 3)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Skipping floor at index {i}: at least 3 points are required to define a floor");
                    continue;
                }

                Level level = ExtractObject<Level>(levelObjs[i], "Level");
                FloorProperties floorProps = ExtractObject<FloorProperties>(propObjs[i], "FloorProperties");

                // Get diaphragm and surface load (if available)
                Diaphragm diaphragm = diaphragmObjs.Count > i ? ExtractObject<Diaphragm>(diaphragmObjs[i], "Diaphragm") : null;
                SurfaceLoad surfaceLoad = surfaceLoadObjs.Count > i ? ExtractObject<SurfaceLoad>(surfaceLoadObjs[i], "SurfaceLoad") : null;
                ShellModifiers etabsModifiers = etabsModObjs.Count > i ? ExtractObject<ShellModifiers>(etabsModObjs[i], "ETABSShellModifiers") : null;

                if (level == null || floorProps == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid level or properties at index {i}");
                    continue;
                }

                // Convert input points to model Point2D objects
                List<Point2D> floorPoints = new List<Point2D>();
                foreach (var ghPoint in pointsBranch)
                {
                    if (ghPoint is GH_Point ghPointCast)
                    {
                        Point3d pt = ghPointCast.Value;
                        floorPoints.Add(new Point2D(pt.X * 12, pt.Y * 12));
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid point at index {i}");
                    }
                }

                // Get span direction (if available)
                double spanDirection = spanDirections.Count > i ? spanDirections[i] : 0.0;

                // Create the floor with all properties
                Floor floor = new Floor
                {
                    LevelId = level.Id,
                    FloorPropertiesId = floorProps.Id,
                    Points = floorPoints,
                    DiaphragmId = diaphragm?.Id,
                    SurfaceLoadId = surfaceLoad?.Id,
                    SpanDirection = spanDirection
                };

                // Apply ETABS modifiers if provided
                if (etabsModifiers != null)
                {
                    floor.ETABSModifiers = etabsModifiers;
                }

                floors.Add(new GH_Floor(floor));
            }

            DA.SetDataList(0, floors);
        }

        private T ExtractObject<T>(object obj, string typeName) where T : class
        {
            // Handle null input
            if (obj == null)
                return null;

            // Direct type check
            if (obj is T directType)
                return directType;

            // Using GooWrapper
            if (obj is GH_ModelGoo<T> ghType)
                return ghType.Value;

            // Try to handle string IDs (for compatibility)
            if (obj is string strId && !string.IsNullOrEmpty(strId))
            {
                if (typeof(T) == typeof(Level))
                    return new Level(strId, null, 0) as T;

                if (typeof(T) == typeof(FloorProperties))
                    return new FloorProperties { Name = strId } as T;

                if (typeof(T) == typeof(Diaphragm))
                    return new Diaphragm { Name = strId } as T;

                if (typeof(T) == typeof(SurfaceLoad))
                    return new SurfaceLoad { Id = strId } as T;
            }

            // If it's a GooWrapper but not for the expected type, log it
            if (obj is IGH_Goo)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Got {obj.GetType().Name} but expected {typeName}");
            }

            return null;
        }

        public override Guid ComponentGuid => new Guid("8D3932A5-1B74-43C6-9BC7-8C0C21B932C5");
    }
}
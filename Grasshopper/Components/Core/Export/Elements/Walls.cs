using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Grasshopper.Utilities;
using Core.Models.Geometry;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class WallCollectorComponent : ComponentBase
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
            pManager.AddGenericParameter("Base Level", "BL", "Base level of the wall", GH_ParamAccess.list);
            pManager.AddGenericParameter("Top Level", "TL", "Top level of the wall", GH_ParamAccess.list);
            pManager.AddGenericParameter("Pier/Spandrel", "PS", "Pier/spandrel configuration (optional)", GH_ParamAccess.list);

            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Walls", "W", "Wall objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Input collection
            List<Curve> curves = new List<Curve>();
            List<object> propObjs = new List<object>();
            List<object> baseLevelObjs = new List<object>();
            List<object> topLevelObjs = new List<object>();
            List<object> pierSpandrelObjs = new List<object>();

            // Check if we have valid inputs
            if (!DA.GetDataList(0, curves))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No curves provided for walls");
                return;
            }

            if (!DA.GetDataList(1, propObjs))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No properties provided for walls");
                return;
            }

            if (!DA.GetDataList(2, baseLevelObjs))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No base levels provided for walls");
                return;
            }

            if (!DA.GetDataList(3, topLevelObjs))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No top levels provided for walls");
                return;
            }

            DA.GetDataList(4, pierSpandrelObjs);

            // Validate input counts
            if (curves.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No curves provided for walls");
                return;
            }

            if (propObjs.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No properties provided for walls");
                return;
            }

            if (baseLevelObjs.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No base levels provided for walls");
                return;
            }

            if (topLevelObjs.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No top levels provided for walls");
                return;
            }

            // Check that the number of items match
            if (curves.Count != propObjs.Count || curves.Count != baseLevelObjs.Count || curves.Count != topLevelObjs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of curves ({curves.Count}) must match number of properties ({propObjs.Count}), " +
                    $"base levels ({baseLevelObjs.Count}), and top levels ({topLevelObjs.Count})");
                return;
            }

            if (pierSpandrelObjs.Count > 0 && pierSpandrelObjs.Count != curves.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"If provided, number of pier/spandrel objects ({pierSpandrelObjs.Count}) must match number of curves ({curves.Count})");
                return;
            }

            List<GH_Wall> walls = new List<GH_Wall>();

            // Process each curve to create a wall
            for (int i = 0; i < curves.Count; i++)
            {
                Curve curve = curves[i];

                // First check curve validity
                if (curve == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Null curve at index {i}");
                    continue;
                }

                if (!curve.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid curve at index {i}");
                    continue;
                }

                // Check if curve has a valid length (avoids division by zero)
                double curveLength = 0;
                try
                {
                    curveLength = curve.GetLength();
                    if (curveLength <= 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Curve at index {i} has zero or negative length");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error measuring curve at index {i}: {ex.Message}");
                    continue;
                }

                // Extract objects from inputs
                WallProperties wallProps = ExtractObject<WallProperties>(propObjs[i], "WallProperties");
                Level baseLevel = ExtractObject<Level>(baseLevelObjs[i], "BaseLevel");
                Level topLevel = ExtractObject<Level>(topLevelObjs[i], "TopLevel");
                object pierSpandrel = pierSpandrelObjs.Count > i ? pierSpandrelObjs[i] : null;

                // Validate extracted objects
                if (wallProps == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid properties at index {i}");
                    continue;
                }

                if (baseLevel == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid base level at index {i}");
                    continue;
                }

                if (topLevel == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid top level at index {i}");
                    continue;
                }

                // Check that top level is above base level
                if (topLevel.Elevation <= baseLevel.Elevation)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Top level ({topLevel.Name}, elevation: {topLevel.Elevation}) must be above base level ({baseLevel.Name}, elevation: {baseLevel.Elevation}) at index {i}");
                    continue;
                }

                // Get start and end points of the curve
                List<Point2D> points = GetStartAndEndPoints(curve);

                // Check if we have enough points to create a wall
                if (points.Count < 2)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Could not extract enough points from curve at index {i}");
                    continue;
                }

                Wall wall = new Wall
                {
                    Points = points,
                    PropertiesId = wallProps.Id,
                    BaseLevelId = baseLevel.Id,
                    TopLevelId = topLevel.Id,
                    PierSpandrelId = pierSpandrel?.ToString()
                };

                walls.Add(new GH_Wall(wall));
            }

            // Return success message
            if (walls.Count > 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    $"Successfully created {walls.Count} wall objects");
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "No walls were created. Check input data and error messages.");
            }

            DA.SetDataList(0, walls);
        }

        private List<Point2D> GetStartAndEndPoints(Curve curve)
        {
            List<Point2D> points = new List<Point2D>();

            // For very short curves, just use start and end points
            Point3d startPoint = curve.PointAtStart;
            Point3d endPoint = curve.PointAtEnd;
            points.Add(new Point2D(startPoint.X * 12, startPoint.Y * 12));
            points.Add(new Point2D(endPoint.X * 12, endPoint.Y * 12));

            return points;
        }

        private T ExtractObject<T>(object obj, string typeName) where T : class
        {
            try
            {
                // Direct type check
                if (obj is T directType)
                    return directType;

                // Using GooWrapper
                if (obj is GH_ModelGoo<T> ghType)
                    return ghType.Value;

                // Try handling string IDs (for compatibility)
                if (obj is string && typeof(T) == typeof(WallProperties))
                {
                    string name = (string)obj;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Empty string ID provided for WallProperties");
                        return null;
                    }
                    return new WallProperties { Name = name } as T;
                }
                else if (obj is string && typeof(T) == typeof(Level))
                {
                    string name = (string)obj;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Empty string ID provided for Level");
                        return null;
                    }
                    return new Level(name, null, 0) as T;
                }

                // Try cast from Grasshopper types
                if (obj is Kernel.Types.IGH_Goo goo)
                {
                    T result = default;
                    if (goo.CastTo(out result))
                        return result;
                }

                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Could not extract {typeName} from object of type {obj?.GetType().Name ?? "null"}");
                return null;
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Error extracting {typeName}: {ex.Message}");
                return null;
            }
        }

        public override Guid ComponentGuid => new Guid("B45C3A75-A162-4F76-8E3A-89F2D8D3C0EC");
    }
}

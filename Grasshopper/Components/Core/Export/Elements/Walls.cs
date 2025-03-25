using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.Properties;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Elements
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
            // Input collection
            List<Curve> curves = new List<Curve>();
            List<object> propObjs = new List<object>();
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

            DA.GetDataList(2, pierSpandrelObjs);

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

            if (curves.Count != propObjs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of curves ({curves.Count}) must match number of properties ({propObjs.Count})");
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

                WallProperties wallProps = ExtractObject<WallProperties>(propObjs[i], "WallProperties");
                object pierSpandrel = pierSpandrelObjs.Count > i ? pierSpandrelObjs[i] : null;

                if (wallProps == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid properties at index {i}");
                    continue;
                }

                // Sample points along the curve to create wall points
                try
                {
                    List<Point2D> points = new List<Point2D>();

                    // Determine appropriate number of points based on curve length
                    // More robust calculation with minimum and maximum limits
                    int numPoints = Math.Max(2, Math.Min(100, (int)(curveLength / 12.0) + 1));

                    // Use curve division with parameter checks
                    if (numPoints == 2)
                    {
                        // For very short curves, just use start and end points
                        Point3d startPoint = curve.PointAtStart;
                        Point3d endPoint = curve.PointAtEnd;
                        points.Add(new Point2D(startPoint.X * 12, startPoint.Y * 12));
                        points.Add(new Point2D(endPoint.X * 12, endPoint.Y * 12));
                    }
                    else
                    {
                        // Sample along the curve at regular intervals
                        for (int j = 0; j < numPoints; j++)
                        {
                            try
                            {
                                double t = (double)j / (numPoints - 1);
                                if (t >= 0 && t <= 1)
                                {
                                    Point3d point = curve.PointAt(t);
                                    points.Add(new Point2D(point.X * 12, point.Y * 12));
                                }
                            }
                            catch (Exception ex)
                            {
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                                    $"Failed to get point on curve at index {i}, parameter {j}: {ex.Message}");
                                // Continue with other points
                            }
                        }
                    }

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
                        PropertiesId = wallProps.Name,
                        PierSpandrelId = pierSpandrel?.ToString()
                    };

                    walls.Add(new GH_Wall(wall));
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Error creating wall at index {i}: {ex.Message}");
                }
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
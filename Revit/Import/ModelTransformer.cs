using System;
using System.Linq;
using System.Diagnostics;
using Core.Models;
using CG = Core.Models.Geometry;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using Core.Utilities;

namespace Revit.Import
{
    /// <summary>
    /// Applies transformations to imported model
    /// </summary>
    public class ModelTransformer
    {
        private readonly ImportContext _context;

        public ModelTransformer(ImportContext context)
        {
            _context = context;
        }

        public void TransformModel(BaseModel model)
        {
            if (_context.TransformationParams == null) return;

            Debug.WriteLine("ModelTransformer: Applying transformations");

            try
            {
                var transform = CalculateTransformation(model);
                if (transform != null)
                {
                    // Debug: Log column positions before transformation
                    if (model.Elements?.Columns != null)
                    {
                        Debug.WriteLine("=== COLUMN POSITIONS BEFORE TRANSFORM ===");
                        foreach (var column in model.Elements.Columns.Take(3)) // Log first 3 columns
                        {
                            Debug.WriteLine($"Column {column.Id}: Start=({column.StartPoint?.X:F2}, {column.StartPoint?.Y:F2}), End=({column.EndPoint?.X:F2}, {column.EndPoint?.Y:F2})");
                        }
                    }

                    ModelTransformation.TransformModel(
                        model,
                        transform.RotationAngle,
                        transform.RotationCenter,
                        transform.Translation
                    );

                    // Debug: Log column positions after transformation
                    if (model.Elements?.Columns != null)
                    {
                        Debug.WriteLine("=== COLUMN POSITIONS AFTER TRANSFORM ===");
                        foreach (var column in model.Elements.Columns.Take(3)) // Log first 3 columns
                        {
                            Debug.WriteLine($"Column {column.Id}: Start=({column.StartPoint?.X:F2}, {column.StartPoint?.Y:F2}), End=({column.EndPoint?.X:F2}, {column.EndPoint?.Y:F2})");
                        }
                    }

                    Debug.WriteLine($"Applied transformation: Rotation={transform.RotationAngle:F2}°, Translation=({transform.Translation.X:F2}, {transform.Translation.Y:F2}, {transform.Translation.Z:F2})");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying transformations: {ex.Message}");
                throw new Exception($"Error applying transformations: {ex.Message}", ex);
            }
        }

        private TransformationResult CalculateTransformation(BaseModel model)
        {
            var result = new TransformationResult();
            var tp = _context.TransformationParams;

            // Grid intersection transformation (using selected grids)
            if (tp.UseGridIntersection)
            {
                var gridTransform = CalculateGridIntersectionTransform(model);
                if (gridTransform != null)
                {
                    result = gridTransform;
                }
            }
            // Manual rotation
            else if (tp.UseManualRotation && Math.Abs(tp.RotationAngle) > 0.001)
            {
                result.RotationAngle = tp.RotationAngle;
                result.RotationCenter = CalculateModelCenter(model);
            }

            // Base level elevation adjustment
            if (Math.Abs(tp.BaseLevelElevation) > 0.001)
            {
                double elevationOffset = tp.BaseLevelElevation * 12.0; // Convert feet to inches
                result.Translation = new CG.Point3D(
                    result.Translation.X,
                    result.Translation.Y,
                    result.Translation.Z + elevationOffset
                );
            }

            return result;
        }

        private TransformationResult CalculateGridIntersectionTransform(BaseModel model)
        {
            try
            {
                Debug.WriteLine("=== STARTING GRID INTERSECTION TRANSFORM ===");

                // Get lower-left intersection from imported model (automatic)
                Debug.WriteLine("Getting model grid intersection...");
                var modelIntersection = GetLowerLeftGridIntersection(model);
                if (modelIntersection.Intersection == null)
                {
                    Debug.WriteLine("ERROR: Could not find lower-left grid intersection in imported model");
                    return null;
                }
                Debug.WriteLine($"Model intersection found: ({modelIntersection.Intersection.X:F2}, {modelIntersection.Intersection.Y:F2})");

                // Get intersection from selected Revit grids
                Debug.WriteLine("Getting selected Revit grid intersection...");
                var revitIntersection = GetSelectedGridIntersection();
                if (revitIntersection.Intersection == null)
                {
                    Debug.WriteLine("ERROR: Could not find intersection of selected Revit grids");
                    return null;
                }
                Debug.WriteLine($"Revit intersection found: ({revitIntersection.Intersection.X:F2}, {revitIntersection.Intersection.Y:F2})");

                // Calculate rotation based on horizontal grid angle difference
                Debug.WriteLine("Calculating rotation difference...");
                double rotationAngle = CalculateGridRotationDifference(model, revitIntersection.HorizontalGridRevit);
                Debug.WriteLine($"Calculated rotation angle: {rotationAngle:F2}°");

                // Calculate translation
                var translation = new CG.Point3D(
                    revitIntersection.Intersection.X - modelIntersection.Intersection.X,
                    revitIntersection.Intersection.Y - modelIntersection.Intersection.Y,
                    0
                );
                Debug.WriteLine($"Calculated translation: ({translation.X:F2}, {translation.Y:F2}, {translation.Z:F2})");

                Debug.WriteLine("=== GRID INTERSECTION TRANSFORM COMPLETE ===");

                return new TransformationResult
                {
                    RotationAngle = rotationAngle,
                    RotationCenter = modelIntersection.Intersection,
                    Translation = translation
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in CalculateGridIntersectionTransform: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private GridIntersectionInfo GetSelectedGridIntersection()
        {
            Debug.WriteLine("--- Getting Selected Grid Intersection ---");

            var tp = _context.TransformationParams;
            if (tp.HorizontalGrid == null || tp.VerticalGrid == null)
            {
                Debug.WriteLine("ERROR: No grids selected in UI");
                return new GridIntersectionInfo();
            }

            try
            {
                Debug.WriteLine($"Selected horizontal grid: '{tp.HorizontalGrid.Name}'");
                Debug.WriteLine($"Selected vertical grid: '{tp.VerticalGrid.Name}'");

                // Calculate intersection of selected grids
                var intersection = CalculateRevitGridIntersection(tp.HorizontalGrid, tp.VerticalGrid);
                if (intersection == null)
                {
                    Debug.WriteLine("ERROR: Could not calculate intersection between selected grids");
                    return new GridIntersectionInfo();
                }

                Debug.WriteLine($"Selected grid intersection calculated: ({intersection.X:F2}, {intersection.Y:F2}) feet");
                Debug.WriteLine($"Selected grid intersection in inches: ({intersection.X * 12.0:F2}, {intersection.Y * 12.0:F2})");

                return new GridIntersectionInfo
                {
                    Intersection = new CG.Point2D(intersection.X * 12.0, intersection.Y * 12.0), // Convert to inches
                    HorizontalGridRevit = tp.HorizontalGrid,
                    VerticalGridRevit = tp.VerticalGrid
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR getting selected grid intersection: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new GridIntersectionInfo();
            }
        }

        private GridIntersectionInfo GetLowerLeftGridIntersection(BaseModel model)
        {
            Debug.WriteLine("--- Getting Model Grid Intersection ---");

            if (model.ModelLayout?.Grids == null || model.ModelLayout.Grids.Count < 2)
            {
                Debug.WriteLine($"ERROR: Not enough grids in model. Count: {model.ModelLayout?.Grids?.Count ?? 0}");
                return new GridIntersectionInfo();
            }

            Debug.WriteLine($"Total model grids: {model.ModelLayout.Grids.Count}");
            foreach (var grid in model.ModelLayout.Grids)
            {
                Debug.WriteLine($"  Grid '{grid.Name}': ({grid.StartPoint.X:F2}, {grid.StartPoint.Y:F2}) to ({grid.EndPoint.X:F2}, {grid.EndPoint.Y:F2})");
            }

            var (horizontalGrids, verticalGrids) = SeparateGridsByDirection(model.ModelLayout.Grids);

            Debug.WriteLine($"Horizontal grids: {horizontalGrids.Count}, Vertical grids: {verticalGrids.Count}");

            if (horizontalGrids.Count == 0 || verticalGrids.Count == 0)
            {
                Debug.WriteLine("ERROR: Missing horizontal or vertical grids");
                return new GridIntersectionInfo();
            }

            // Find bottommost horizontal grid and leftmost vertical grid
            var bottomGrid = horizontalGrids.OrderBy(g => Math.Min(g.StartPoint.Y, g.EndPoint.Y)).First();
            var leftGrid = verticalGrids.OrderBy(g => Math.Min(g.StartPoint.X, g.EndPoint.X)).First();

            Debug.WriteLine($"Bottom grid: '{bottomGrid.Name}' - Y: {Math.Min(bottomGrid.StartPoint.Y, bottomGrid.EndPoint.Y):F2}");
            Debug.WriteLine($"Left grid: '{leftGrid.Name}' - X: {Math.Min(leftGrid.StartPoint.X, leftGrid.EndPoint.X):F2}");

            var intersection = CalculateLineIntersection(bottomGrid, leftGrid);
            if (intersection == null)
            {
                Debug.WriteLine("ERROR: Could not calculate intersection between bottom and left grids");
                return new GridIntersectionInfo();
            }

            Debug.WriteLine($"Model intersection calculated: ({intersection.X:F2}, {intersection.Y:F2})");

            return new GridIntersectionInfo
            {
                Intersection = intersection,
                HorizontalGrid = bottomGrid,
                VerticalGrid = leftGrid
            };
        }

        private double CalculateGridRotationDifference(BaseModel model, Autodesk.Revit.DB.Grid revitHorizontalGrid)
        {
            Debug.WriteLine("--- Calculating Grid Rotation Difference ---");

            try
            {
                // Get horizontal grids from model
                var modelHorizontalGrids = SeparateGridsByDirection(model.ModelLayout.Grids).Item1;
                if (modelHorizontalGrids.Count == 0)
                {
                    Debug.WriteLine("ERROR: No horizontal grids in model");
                    return 0.0;
                }

                // Use the bottom-most grid from model (should match the one used for intersection)
                var modelHorizontalGrid = modelHorizontalGrids.OrderBy(g => Math.Min(g.StartPoint.Y, g.EndPoint.Y)).First();

                // Calculate model grid angle (ensure consistent direction - left to right)
                var modelStart = modelHorizontalGrid.StartPoint;
                var modelEnd = modelHorizontalGrid.EndPoint;

                // Always calculate from left to right for consistency
                if (modelStart.X > modelEnd.X)
                {
                    var temp = modelStart;
                    modelStart = modelEnd;
                    modelEnd = temp;
                }

                var modelVector = new CG.Point2D(
                    modelEnd.X - modelStart.X,
                    modelEnd.Y - modelStart.Y
                );
                double modelAngle = Math.Atan2(modelVector.Y, modelVector.X) * 180.0 / Math.PI;

                // Calculate Revit grid angle (ensure consistent direction - left to right)
                if (revitHorizontalGrid.Curve is Line revitLine)
                {
                    var revitStart = revitLine.GetEndPoint(0);
                    var revitEnd = revitLine.GetEndPoint(1);

                    // Always calculate from left to right for consistency
                    if (revitStart.X > revitEnd.X)
                    {
                        var temp = revitStart;
                        revitStart = revitEnd;
                        revitEnd = temp;
                    }

                    var revitVector = new CG.Point2D(revitEnd.X - revitStart.X, revitEnd.Y - revitStart.Y);
                    double revitAngle = Math.Atan2(revitVector.Y, revitVector.X) * 180.0 / Math.PI;

                    // Calculate rotation needed to align model grid to Revit grid
                    double rotationDifference = revitAngle - modelAngle;

                    // Normalize to [-180, 180] range
                    while (rotationDifference > 180) rotationDifference -= 360;
                    while (rotationDifference < -180) rotationDifference += 360;

                    Debug.WriteLine($"Model grid '{modelHorizontalGrid.Name}' angle: {modelAngle:F2}°");
                    Debug.WriteLine($"Revit grid '{revitHorizontalGrid.Name}' angle: {revitAngle:F2}°");
                    Debug.WriteLine($"Rotation difference: {rotationDifference:F2}°");

                    return rotationDifference;
                }

                Debug.WriteLine("ERROR: Revit grid is not a line");
                return 0.0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR calculating rotation difference: {ex.Message}");
                return 0.0;
            }
        }

        #region Helper Methods

        private (List<Core.Models.ModelLayout.Grid>, List<Core.Models.ModelLayout.Grid>) SeparateGridsByDirection(
            IEnumerable<Core.Models.ModelLayout.Grid> grids)
        {
            var horizontal = new List<Core.Models.ModelLayout.Grid>();
            var vertical = new List<Core.Models.ModelLayout.Grid>();

            foreach (var grid in grids)
            {
                var vector = new CG.Point2D(
                    grid.EndPoint.X - grid.StartPoint.X,
                    grid.EndPoint.Y - grid.StartPoint.Y
                );

                // If X component is larger, it's more horizontal
                if (Math.Abs(vector.X) > Math.Abs(vector.Y))
                {
                    horizontal.Add(grid);
                }
                else
                {
                    vertical.Add(grid);
                }
            }

            return (horizontal, vertical);
        }

        private CG.Point2D CalculateLineIntersection(Core.Models.ModelLayout.Grid grid1, Core.Models.ModelLayout.Grid grid2)
        {
            // Line 1: grid1.StartPoint to grid1.EndPoint
            double x1 = grid1.StartPoint.X, y1 = grid1.StartPoint.Y;
            double x2 = grid1.EndPoint.X, y2 = grid1.EndPoint.Y;

            // Line 2: grid2.StartPoint to grid2.EndPoint
            double x3 = grid2.StartPoint.X, y3 = grid2.StartPoint.Y;
            double x4 = grid2.EndPoint.X, y4 = grid2.EndPoint.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denom) < 1e-10) return null; // Lines are parallel

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;

            return new CG.Point2D(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
        }

        private CG.Point2D CalculateRevitGridIntersection(Autodesk.Revit.DB.Grid grid1, Autodesk.Revit.DB.Grid grid2)
        {
            if (!(grid1.Curve is Line line1) || !(grid2.Curve is Line line2))
                return null;

            IntersectionResultArray intersections;
            if (line1.Intersect(line2, out intersections) == SetComparisonResult.Overlap && intersections?.Size > 0)
            {
                var intersection = intersections.get_Item(0);
                return new CG.Point2D(intersection.XYZPoint.X, intersection.XYZPoint.Y);
            }

            return null;
        }

        private CG.Point2D CalculateModelCenter(BaseModel model)
        {
            var points = new List<CG.Point2D>();

            // Collect from grids
            if (model.ModelLayout?.Grids != null)
            {
                foreach (var grid in model.ModelLayout.Grids)
                {
                    if (grid.StartPoint != null) points.Add(new CG.Point2D(grid.StartPoint.X, grid.StartPoint.Y));
                    if (grid.EndPoint != null) points.Add(new CG.Point2D(grid.EndPoint.X, grid.EndPoint.Y));
                }
            }

            // Collect from elements
            if (model.Elements != null)
            {
                if (model.Elements.Beams != null)
                    foreach (var beam in model.Elements.Beams)
                    {
                        if (beam.StartPoint != null) points.Add(beam.StartPoint);
                        if (beam.EndPoint != null) points.Add(beam.EndPoint);
                    }

                if (model.Elements.Columns != null)
                    foreach (var column in model.Elements.Columns)
                    {
                        if (column.StartPoint != null) points.Add(column.StartPoint);
                    }
            }

            if (points.Count == 0) return new CG.Point2D(0, 0);

            return new CG.Point2D(points.Average(p => p.X), points.Average(p => p.Y));
        }

        #endregion

        private struct GridIntersectionInfo
        {
            public CG.Point2D Intersection { get; set; }
            public Core.Models.ModelLayout.Grid HorizontalGrid { get; set; }
            public Core.Models.ModelLayout.Grid VerticalGrid { get; set; }
            public Autodesk.Revit.DB.Grid HorizontalGridRevit { get; set; }
            public Autodesk.Revit.DB.Grid VerticalGridRevit { get; set; }

            public GridIntersectionInfo(CG.Point2D intersection)
            {
                Intersection = intersection;
                HorizontalGrid = null;
                VerticalGrid = null;
                HorizontalGridRevit = null;
                VerticalGridRevit = null;
            }
        }

        private class TransformationResult
        {
            public double RotationAngle { get; set; }
            public CG.Point2D RotationCenter { get; set; } = new CG.Point2D(0, 0);
            public CG.Point3D Translation { get; set; } = new CG.Point3D(0, 0, 0);
        }
    }
}
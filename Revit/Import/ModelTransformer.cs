using System;
using System.Linq;
using System.Diagnostics;
using Core.Models;
using CG = Core.Models.Geometry;
using Autodesk.Revit.DB;
using System.Collections.Generic;

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
                    Core.Models.ModelTransformation.TransformModel(
                        model,
                        transform.RotationAngle,
                        transform.RotationCenter,
                        transform.Translation
                    );

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

            // Auto grid intersection transformation
            if (tp.UseGridIntersection)
            {
                var gridTransform = CalculateAutoGridIntersectionTransform(model);
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

        private TransformationResult CalculateAutoGridIntersectionTransform(BaseModel model)
        {
            try
            {
                Debug.WriteLine("=== STARTING AUTO GRID INTERSECTION TRANSFORM ===");

                // Get lower-left intersection from imported model
                Debug.WriteLine("Getting model grid intersection...");
                var modelIntersection = GetLowerLeftGridIntersection(model);
                if (modelIntersection == null)
                {
                    Debug.WriteLine("ERROR: Could not find lower-left grid intersection in imported model");
                    return null;
                }
                Debug.WriteLine($"Model intersection found: ({modelIntersection.Value.Intersection.X:F2}, {modelIntersection.Value.Intersection.Y:F2})");

                // Get lower-left intersection from Revit
                Debug.WriteLine("Getting Revit grid intersection...");
                var revitIntersection = GetRevitLowerLeftGridIntersection();
                if (revitIntersection == null)
                {
                    Debug.WriteLine("ERROR: Could not find lower-left grid intersection in Revit model");
                    return null;
                }
                Debug.WriteLine($"Revit intersection found: ({revitIntersection.Value.Intersection.X:F2}, {revitIntersection.Value.Intersection.Y:F2})");

                // Calculate rotation based on horizontal grid angle difference
                Debug.WriteLine("Calculating rotation difference...");
                double rotationAngle = CalculateGridRotationDifference(model);
                Debug.WriteLine($"Calculated rotation angle: {rotationAngle:F2}°");

                // Calculate translation
                var translation = new CG.Point3D(
                    revitIntersection.Value.Intersection.X - modelIntersection.Value.Intersection.X,
                    revitIntersection.Value.Intersection.Y - modelIntersection.Value.Intersection.Y,
                    0
                );
                Debug.WriteLine($"Calculated translation: ({translation.X:F2}, {translation.Y:F2}, {translation.Z:F2})");

                Debug.WriteLine("=== AUTO GRID INTERSECTION TRANSFORM COMPLETE ===");

                return new TransformationResult
                {
                    RotationAngle = rotationAngle,
                    RotationCenter = modelIntersection.Value.Intersection,
                    Translation = translation
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in CalculateAutoGridIntersectionTransform: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private GridIntersectionInfo? GetLowerLeftGridIntersection(BaseModel model)
        {
            Debug.WriteLine("--- Getting Model Grid Intersection ---");

            if (model.ModelLayout?.Grids == null || model.ModelLayout.Grids.Count < 2)
            {
                Debug.WriteLine($"ERROR: Not enough grids in model. Count: {model.ModelLayout?.Grids?.Count ?? 0}");
                return null;
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
                return null;
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
                return null;
            }

            Debug.WriteLine($"Model intersection calculated: ({intersection.X:F2}, {intersection.Y:F2})");

            return new GridIntersectionInfo
            {
                Intersection = intersection,
                HorizontalGrid = bottomGrid,
                VerticalGrid = leftGrid
            };
        }

        private GridIntersectionInfo? GetRevitLowerLeftGridIntersection()
        {
            Debug.WriteLine("--- Getting Revit Grid Intersection ---");

            try
            {
                var collector = new FilteredElementCollector(_context.RevitDoc);
                var revitGrids = collector.OfClass(typeof(Autodesk.Revit.DB.Grid))
                    .Cast<Autodesk.Revit.DB.Grid>()
                    .Where(g => g.Curve is Line)
                    .ToList();

                Debug.WriteLine($"Total Revit grids found: {revitGrids.Count}");
                foreach (var grid in revitGrids)
                {
                    if (grid.Curve is Line line)
                    {
                        var start = line.GetEndPoint(0);
                        var end = line.GetEndPoint(1);
                        Debug.WriteLine($"  Grid '{grid.Name}': ({start.X:F2}, {start.Y:F2}) to ({end.X:F2}, {end.Y:F2})");
                    }
                }

                if (revitGrids.Count < 2)
                {
                    Debug.WriteLine("ERROR: Not enough Revit grids found");
                    return null;
                }

                var (horizontalGrids, verticalGrids) = SeparateRevitGridsByDirection(revitGrids);

                Debug.WriteLine($"Revit Horizontal grids: {horizontalGrids.Count}, Vertical grids: {verticalGrids.Count}");

                if (horizontalGrids.Count == 0 || verticalGrids.Count == 0)
                {
                    Debug.WriteLine("ERROR: Missing horizontal or vertical Revit grids");
                    return null;
                }

                // Find bottommost horizontal and leftmost vertical
                var bottomGrid = horizontalGrids.OrderBy(g => GetRevitGridMinY(g)).First();
                var leftGrid = verticalGrids.OrderBy(g => GetRevitGridMinX(g)).First();

                Debug.WriteLine($"Revit Bottom grid: '{bottomGrid.Name}' - Y: {GetRevitGridMinY(bottomGrid):F2}");
                Debug.WriteLine($"Revit Left grid: '{leftGrid.Name}' - X: {GetRevitGridMinX(leftGrid):F2}");

                var intersection = CalculateRevitGridIntersection(bottomGrid, leftGrid);
                if (intersection == null)
                {
                    Debug.WriteLine("ERROR: Could not calculate Revit grid intersection");
                    return null;
                }

                Debug.WriteLine($"Revit intersection calculated: ({intersection.X:F2}, {intersection.Y:F2}) feet");
                Debug.WriteLine($"Revit intersection in inches: ({intersection.X * 12.0:F2}, {intersection.Y * 12.0:F2})");

                return new GridIntersectionInfo
                {
                    Intersection = new CG.Point2D(intersection.X * 12.0, intersection.Y * 12.0), // Convert to inches
                    HorizontalGridRevit = bottomGrid,
                    VerticalGridRevit = leftGrid
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR getting Revit lower-left grid intersection: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private double CalculateGridRotationDifference(BaseModel model)
        {
            Debug.WriteLine("--- Calculating Grid Rotation Difference ---");

            try
            {
                // Get horizontal grids from both models
                var modelHorizontalGrids = SeparateGridsByDirection(model.ModelLayout.Grids).Item1;
                var revitGrids = new FilteredElementCollector(_context.RevitDoc)
                    .OfClass(typeof(Autodesk.Revit.DB.Grid))
                    .Cast<Autodesk.Revit.DB.Grid>()
                    .Where(g => g.Curve is Line)
                    .ToList();
                var revitHorizontalGrids = SeparateRevitGridsByDirection(revitGrids).Item1;

                Debug.WriteLine($"Model horizontal grids count: {modelHorizontalGrids.Count}");
                Debug.WriteLine($"Revit horizontal grids count: {revitHorizontalGrids.Count}");

                if (modelHorizontalGrids.Count == 0 || revitHorizontalGrids.Count == 0)
                {
                    Debug.WriteLine("ERROR: No horizontal grids found in one or both models");
                    return 0;
                }

                // Use the first horizontal grid from each model
                var modelGrid = modelHorizontalGrids.First();
                var revitGrid = revitHorizontalGrids.First();

                // Calculate angles
                double modelAngle = CalculateGridAngle(modelGrid);
                double revitAngle = CalculateRevitGridAngle(revitGrid);

                Debug.WriteLine($"Model grid '{modelGrid.Name}' angle: {modelAngle:F2}°");
                Debug.WriteLine($"Revit grid '{revitGrid.Name}' angle: {revitAngle:F2}°");

                // Return the difference
                double angleDiff = revitAngle - modelAngle;
                Debug.WriteLine($"Raw angle difference: {angleDiff:F2}°");

                // Normalize to [-180, 180] range
                while (angleDiff > 180) angleDiff -= 360;
                while (angleDiff < -180) angleDiff += 360;

                Debug.WriteLine($"Normalized angle difference: {angleDiff:F2}°");
                return angleDiff;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR calculating rotation difference: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return 0;
            }
        }

        #region Helper Methods

        private (List<Core.Models.ModelLayout.Grid>, List<Core.Models.ModelLayout.Grid>) SeparateGridsByDirection(
            List<Core.Models.ModelLayout.Grid> grids)
        {
            Debug.WriteLine("--- Separating Model Grids by Direction ---");

            var horizontal = new List<Core.Models.ModelLayout.Grid>();
            var vertical = new List<Core.Models.ModelLayout.Grid>();

            foreach (var grid in grids)
            {
                double deltaX = Math.Abs(grid.EndPoint.X - grid.StartPoint.X);
                double deltaY = Math.Abs(grid.EndPoint.Y - grid.StartPoint.Y);

                Debug.WriteLine($"Grid '{grid.Name}': ΔX={deltaX:F2}, ΔY={deltaY:F2}");

                if (deltaX > deltaY)
                {
                    horizontal.Add(grid);
                    Debug.WriteLine($"  → Classified as HORIZONTAL");
                }
                else
                {
                    vertical.Add(grid);
                    Debug.WriteLine($"  → Classified as VERTICAL");
                }
            }

            Debug.WriteLine($"Result: {horizontal.Count} horizontal, {vertical.Count} vertical grids");
            return (horizontal, vertical);
        }

        private (List<Autodesk.Revit.DB.Grid>, List<Autodesk.Revit.DB.Grid>) SeparateRevitGridsByDirection(
            List<Autodesk.Revit.DB.Grid> grids)
        {
            Debug.WriteLine("--- Separating Revit Grids by Direction ---");

            var horizontal = new List<Autodesk.Revit.DB.Grid>();
            var vertical = new List<Autodesk.Revit.DB.Grid>();

            foreach (var grid in grids)
            {
                if (!(grid.Curve is Line line)) continue;

                var start = line.GetEndPoint(0);
                var end = line.GetEndPoint(1);

                double deltaX = Math.Abs(end.X - start.X);
                double deltaY = Math.Abs(end.Y - start.Y);

                Debug.WriteLine($"Revit Grid '{grid.Name}': ΔX={deltaX:F2}, ΔY={deltaY:F2}");

                if (deltaX > deltaY)
                {
                    horizontal.Add(grid);
                    Debug.WriteLine($"  → Classified as HORIZONTAL");
                }
                else
                {
                    vertical.Add(grid);
                    Debug.WriteLine($"  → Classified as VERTICAL");
                }
            }

            Debug.WriteLine($"Result: {horizontal.Count} horizontal, {vertical.Count} vertical Revit grids");
            return (horizontal, vertical);
        }

        private double GetRevitGridMinY(Autodesk.Revit.DB.Grid grid)
        {
            if (!(grid.Curve is Line line)) return 0;
            var start = line.GetEndPoint(0);
            var end = line.GetEndPoint(1);
            return Math.Min(start.Y, end.Y);
        }

        private double GetRevitGridMinX(Autodesk.Revit.DB.Grid grid)
        {
            if (!(grid.Curve is Line line)) return 0;
            var start = line.GetEndPoint(0);
            var end = line.GetEndPoint(1);
            return Math.Min(start.X, end.X);
        }

        private double CalculateGridAngle(Core.Models.ModelLayout.Grid grid)
        {
            double deltaX = grid.EndPoint.X - grid.StartPoint.X;
            double deltaY = grid.EndPoint.Y - grid.StartPoint.Y;
            return Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI;
        }

        private double CalculateRevitGridAngle(Autodesk.Revit.DB.Grid grid)
        {
            if (!(grid.Curve is Line line)) return 0;
            var start = line.GetEndPoint(0);
            var end = line.GetEndPoint(1);
            double deltaX = end.X - start.X;
            double deltaY = end.Y - start.Y;
            return Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI;
        }

        private CG.Point2D CalculateLineIntersection(Core.Models.ModelLayout.Grid grid1, Core.Models.ModelLayout.Grid grid2)
        {
            double x1 = grid1.StartPoint.X, y1 = grid1.StartPoint.Y;
            double x2 = grid1.EndPoint.X, y2 = grid1.EndPoint.Y;
            double x3 = grid2.StartPoint.X, y3 = grid2.StartPoint.Y;
            double x4 = grid2.EndPoint.X, y4 = grid2.EndPoint.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denom) < 1e-10) return null;

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
            public CG.Point2D Intersection;
            public Core.Models.ModelLayout.Grid HorizontalGrid;
            public Core.Models.ModelLayout.Grid VerticalGrid;
            public Autodesk.Revit.DB.Grid HorizontalGridRevit;
            public Autodesk.Revit.DB.Grid VerticalGridRevit;
        }

        private class TransformationResult
        {
            public double RotationAngle { get; set; }
            public CG.Point2D RotationCenter { get; set; } = new CG.Point2D(0, 0);
            public CG.Point3D Translation { get; set; } = new CG.Point3D(0, 0, 0);
        }
    }
}
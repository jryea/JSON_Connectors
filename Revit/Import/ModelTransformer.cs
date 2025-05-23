using System;
using System.Linq;
using System.Diagnostics;
using Core.Models;
using Core.Models.Geometry;
using Autodesk.Revit.DB;

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

            // Grid intersection transformation
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
                result.Translation = new Point3D(
                    result.Translation.X,
                    result.Translation.Y,
                    result.Translation.Z + elevationOffset
                );
            }

            return result;
        }

        private TransformationResult CalculateGridIntersectionTransform(BaseModel model)
        {
            var tp = _context.TransformationParams;

            // Get intersection in imported model
            var importedIntersection = GetGridIntersectionPoint(model,
                tp.UseImportedGrids ? tp.ImportedGrid1Name : tp.Grid1Name,
                tp.UseImportedGrids ? tp.ImportedGrid2Name : tp.Grid2Name);

            if (importedIntersection == null)
            {
                Debug.WriteLine("Could not find grid intersection in imported model");
                return null;
            }

            // Get intersection in Revit
            var revitIntersection = GetRevitGridIntersectionPoint(tp.Grid1Name, tp.Grid2Name);
            if (revitIntersection == null)
            {
                Debug.WriteLine("Could not find grid intersection in Revit model");
                return null;
            }

            return new TransformationResult
            {
                RotationAngle = 0,
                RotationCenter = importedIntersection,
                Translation = new Point3D(
                    revitIntersection.X - importedIntersection.X,
                    revitIntersection.Y - importedIntersection.Y,
                    0
                )
            };
        }

        private Point2D GetGridIntersectionPoint(BaseModel model, string grid1Name, string grid2Name)
        {
            if (model.ModelLayout?.Grids == null) return null;

            var grid1 = model.ModelLayout.Grids.FirstOrDefault(g => g.Name == grid1Name);
            var grid2 = model.ModelLayout.Grids.FirstOrDefault(g => g.Name == grid2Name);

            if (grid1 == null || grid2 == null) return null;

            return CalculateLineIntersection(grid1, grid2);
        }

        private Point2D GetRevitGridIntersectionPoint(string grid1Name, string grid2Name)
        {
            try
            {
                var collector = new FilteredElementCollector(_context.RevitDoc);
                var grids = collector.OfClass(typeof(Autodesk.Revit.DB.Grid))
                    .Cast<Autodesk.Revit.DB.Grid>()
                    .ToList();

                var grid1 = grids.FirstOrDefault(g => g.Name == grid1Name);
                var grid2 = grids.FirstOrDefault(g => g.Name == grid2Name);

                if (grid1?.Curve is Autodesk.Revit.DB.Line line1 && grid2?.Curve is Autodesk.Revit.DB.Line line2)
                {
                    IntersectionResultArray intersections;
                    if (line1.Intersect(line2, out intersections) == SetComparisonResult.Overlap &&
                        intersections?.Size > 0)
                    {
                        var intersection = intersections.get_Item(0);
                        return new Point2D(
                            intersection.XYZPoint.X * 12.0, // Convert to inches
                            intersection.XYZPoint.Y * 12.0
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Revit grid intersection: {ex.Message}");
            }

            return null;
        }

        private Point2D CalculateLineIntersection(Core.Models.ModelLayout.Grid grid1, Core.Models.ModelLayout.Grid grid2)
        {
            double x1 = grid1.StartPoint.X, y1 = grid1.StartPoint.Y;
            double x2 = grid1.EndPoint.X, y2 = grid1.EndPoint.Y;
            double x3 = grid2.StartPoint.X, y3 = grid2.StartPoint.Y;
            double x4 = grid2.EndPoint.X, y4 = grid2.EndPoint.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denom) < 1e-10) return null;

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            return new Point2D(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
        }

        private Point2D CalculateModelCenter(BaseModel model)
        {
            var points = new System.Collections.Generic.List<Point2D>();

            // Collect from grids
            if (model.ModelLayout?.Grids != null)
            {
                foreach (var grid in model.ModelLayout.Grids)
                {
                    if (grid.StartPoint != null) points.Add(new Point2D(grid.StartPoint.X, grid.StartPoint.Y));
                    if (grid.EndPoint != null) points.Add(new Point2D(grid.EndPoint.X, grid.EndPoint.Y));
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

            if (points.Count == 0) return new Point2D(0, 0);

            return new Point2D(points.Average(p => p.X), points.Average(p => p.Y));
        }

        private class TransformationResult
        {
            public double RotationAngle { get; set; }
            public Point2D RotationCenter { get; set; } = new Point2D(0, 0);
            public Point3D Translation { get; set; } = new Point3D(0, 0, 0);
        }
    }
}
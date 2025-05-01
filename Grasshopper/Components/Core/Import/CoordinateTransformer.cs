using Core.Models;
using Rhino.Geometry;
using System.Collections.Generic;
using System;

namespace Grasshopper.Components.Core.Import
{
    internal class CoordinateTransformer
    {
        private BaseModel _model;
        private Point3d _basePoint;
        private double _rotation;

        public CoordinateTransformer(BaseModel model)
        {
            _model = model;

            // Initialize from model coordinate data
            if (_model?.Metadata?.Coordinates != null)
            {
                var coords = _model.Metadata.Coordinates;
                _basePoint = new Point3d(
                    coords.ProjectBasePoint?.X ?? 0,
                    coords.ProjectBasePoint?.Y ?? 0,
                    coords.ProjectBasePoint?.Z ?? 0);

                _rotation = coords.Rotation;
            }
            else
            {
                _basePoint = Point3d.Origin;
                _rotation = 0;
            }
        }

        public Point3d TransformPoint(double x, double y, double z)
        {
            // Translate
            double xTranslated = x - _basePoint.X;
            double yTranslated = y - _basePoint.Y;
            double zTranslated = z - _basePoint.Z;

            // Rotate
            double xRotated = xTranslated * Math.Cos(_rotation) - yTranslated * Math.Sin(_rotation);
            double yRotated = xTranslated * Math.Sin(_rotation) + yTranslated * Math.Cos(_rotation);

            return new Point3d(xRotated, yRotated, zTranslated);
        }

        public List<Line> CreateGridGeometry()
        {
            List<Line> gridLines = new List<Line>();

            foreach (var grid in _model.ModelLayout.Grids)
            {
                Point3d startPoint = TransformPoint(
                    grid.StartPoint.X, grid.StartPoint.Y, grid.StartPoint.Z);

                Point3d endPoint = TransformPoint(
                    grid.EndPoint.X, grid.EndPoint.Y, grid.EndPoint.Z);

                gridLines.Add(new Line(startPoint, endPoint));
            }

            return gridLines;
        }

        public List<Rectangle3d> CreateLevelGeometry()
        {
            List<Rectangle3d> levelPlanes = new List<Rectangle3d>();

            // Implementation to create level geometries

            return levelPlanes;
        }

        public List<Curve> CreateFloorGeometry()
        {
            List<Curve> floorCurves = new List<Curve>();

            // Implementation to create floor geometries

            return floorCurves;
        }
    }
}
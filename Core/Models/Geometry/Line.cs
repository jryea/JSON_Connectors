using System;

namespace Core.Models.Geometry
{
    // Represents a 3D line segment defined by start and end points
    public class Line
    {
      
        public double FromX { get; set; }

        // Y-coordinate of the start point
        public double FromY { get; set; }

        // Z-coordinate of the start point
        public double FromZ { get; set; }

        // X-coordinate of the end point
        public double ToX { get; set; }

        // Y-coordinate of the end point
        public double ToY { get; set; }

        // Z-coordinate of the end point
        public double ToZ { get; set; }

        // Creates a new Line with the specified start and end points
        public Line(double fromX, double fromY, double fromZ, double toX, double toY, double toZ)
        {
            FromX = fromX;
            FromY = fromY;
            FromZ = fromZ;
            ToX = toX;
            ToY = toY;
            ToZ = toZ;
        }

        // Creates a new Line with the specified start and end points (2D, Z=0)
        public Line(double fromX, double fromY, double toX, double toY)
            : this(fromX, fromY, 0, toX, toY, 0)
        {
        }

        // Creates a new Line with the specified Point2D start and end points
        public Line(Point2D fromPoint, Point2D toPoint)
            : this(fromPoint.X, fromPoint.Y, 0, toPoint.X, toPoint.Y, 0)
        {
        }

        // Creates a new Line with the specified Point3D start and end points
        public Line(Point3D fromPoint, Point3D toPoint)
            : this(fromPoint.X, fromPoint.Y, fromPoint.Z, toPoint.X, toPoint.Y, toPoint.Z)
        {
        }

        // Gets the length of the line
        public double Length
        {
            get
            {
                return Math.Sqrt(
                    Math.Pow(ToX - FromX, 2) +
                    Math.Pow(ToY - FromY, 2) +
                    Math.Pow(ToZ - FromZ, 2));
            }
        }

        // Gets the mid-point of the line
    
        public Point3D MidPoint()
        {
            return new Point3D(
                (FromX + ToX) / 2,
                (FromY + ToY) / 2,
                (FromZ + ToZ) / 2);
        }

        // Returns a string representation of the line
    
        public override string ToString()
        {
            return $"Line: ({FromX},{FromY},{FromZ}) to ({ToX},{ToY},{ToZ})";
        }
    }
}
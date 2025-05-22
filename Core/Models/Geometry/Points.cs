using System;
using System.Collections.Generic;

namespace Core.Models.Geometry
{
    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        internal void Rotate(double angleDegrees, Point2D center)
        {
            double angleRad = angleDegrees * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            // Translate to origin
            double dx = X - center.X;
            double dy = Y - center.Y;

            // Apply rotation
            double newX = dx * cos - dy * sin;
            double newY = dx * sin + dy * cos;

            // Translate back
            X = newX + center.X;
            Y = newY + center.Y;
        }

        internal void Translate(Point3D offset)
        {
            X += offset.X;
            Y += offset.Y;
            // Ignore Z component for 2D point
        }
    }

    public class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal void Rotate(double angleDegrees, Point2D center)
        {
            double angleRad = angleDegrees * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            // Translate to origin (XY only)
            double dx = X - center.X;
            double dy = Y - center.Y;

            // Apply rotation (XY only)
            double newX = dx * cos - dy * sin;
            double newY = dx * sin + dy * cos;

            // Translate back
            X = newX + center.X;
            Y = newY + center.Y;
            // Z remains unchanged
        }

        internal void Translate(Point3D offset)
        {
            X += offset.X;
            Y += offset.Y;
            Z += offset.Z;
        }
    }

    public class GridPoint : Point3D
    {
        public bool IsBubble { get; set; }

        public GridPoint(double x, double y, double z, bool isBubble = true)
            : base(x, y, z)
        {
            IsBubble = isBubble;
        }

        // Inherits transformation methods from Point3D
    }
}
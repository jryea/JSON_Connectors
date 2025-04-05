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
    }

    public class GridPoint : Point3D
    {
        public bool IsBubble { get; set; }

        public GridPoint(double x, double y, double z, bool isBubble = true)
            : base(x, y, z)
        {
            IsBubble = isBubble;
        }
    }
}
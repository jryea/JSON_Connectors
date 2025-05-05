using Core.Models.Geometry;

namespace Core.Models.Metadata
{
    public class Coordinates
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Rotation { get; set; } // Rotation angle from project north

        // Project Base Point
        public Point3D ProjectBasePoint { get; set; }

        // Survey Point
        public Point3D SurveyPoint { get; set; }

        // Custom User Selected Coordination Point
        public Point3D CoordinationPoint { get; set; }

    }
}
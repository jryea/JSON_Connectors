using Core.Models.Geometry;

namespace Core.Models.Metadata
{
    public class Coordinates : ITransformable
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

        // ITransformable implementation
        public void Rotate(double angleDegrees, Point2D center)
        {
            // Transform individual coordinate properties
            Point2D tempPoint = new Point2D(X, Y);
            tempPoint.Rotate(angleDegrees, center);
            X = tempPoint.X;
            Y = tempPoint.Y;

            // Transform reference points
            ProjectBasePoint?.Rotate(angleDegrees, center);
            SurveyPoint?.Rotate(angleDegrees, center);
            CoordinationPoint?.Rotate(angleDegrees, center);

            // Update rotation value
            Rotation += angleDegrees;
            // Normalize to 0-360 range
            while (Rotation >= 360.0) Rotation -= 360.0;
            while (Rotation < 0.0) Rotation += 360.0;
        }

        public void Translate(Point3D offset)
        {
            X += offset.X;
            Y += offset.Y;
            Z += offset.Z;

            // Transform reference points
            ProjectBasePoint?.Translate(offset);
            SurveyPoint?.Translate(offset);
            CoordinationPoint?.Translate(offset);
        }
    }
}
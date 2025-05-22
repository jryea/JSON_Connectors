using Core.Models.Geometry;

namespace Core.Models
{
    // Interface for objects that can be geometrically transformed
    public interface ITransformable
    {
        // Rotates the object around a center point in the XY plane
        void Rotate(double angleDegrees, Point2D center);

        // Translates the object by the specified offset
        void Translate(Point3D offset);
    }
} 
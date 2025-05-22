using Core.Models.Geometry;
using Core.Models;
using Core.Utilities;

namespace Core.Models.Elements
{
    public class IsolatedFooting : IIdentifiable, ITransformable
    {
        // Unique identifier for the isolated footing
        public string Id { get; set; }

        // Width of the footing
        public double Width { get; set; }

        // Length of the footing
        public double Length { get; set; }

        // Thickness of the footing
        public double Thickness { get; set; }

        // Location of the footing
        public Point3D Point { get; set; }

        // ID of the level this footing belongs to
        public string LevelId { get; set; }

        // Material Id
        public string MaterialId { get; set; }

        // Creates a new IsolatedFooting with a generated ID
        public IsolatedFooting()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.ISOLATED_FOOTING);
        }

        // ITransformable implementation
        public void Rotate(double angleDegrees, Point2D center)
        {
            Point?.Rotate(angleDegrees, center);
        }

        public void Translate(Point3D offset)
        {
            Point?.Translate(offset);
        }
    }
}
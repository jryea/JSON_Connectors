using Core.Models.Geometry;
using Core.Utilities;

namespace Core.Models.ModelLayout
{
    // Represents a grid in the structural model
    public class Grid : IIdentifiable, ITransformable
    {
        // Unique identifier for the grid
        public string Id { get; set; }

        // Name of the grid
        public string Name { get; set; }

        // Starting point of the grid
        public GridPoint StartPoint { get; set; }

        // Ending point of the grid
        public GridPoint EndPoint { get; set; }

        // Creates a new Grid with a generated ID
        public Grid()
        {
            Id = IdGenerator.Generate(IdGenerator.Layout.GRID);
        }

        // Creates a new Grid with the specified properties
        public Grid(string name, GridPoint startPoint, GridPoint endPoint) : this()
        {
            Name = name;
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        // ITransformable implementation
        public void Rotate(double angleDegrees, Point2D center)
        {
            StartPoint?.Rotate(angleDegrees, center);
            EndPoint?.Rotate(angleDegrees, center);
        }

        public void Translate(Point3D offset)
        {
            StartPoint?.Translate(offset);
            EndPoint?.Translate(offset);
        }
    }
}
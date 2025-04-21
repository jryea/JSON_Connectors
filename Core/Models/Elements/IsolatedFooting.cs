using Core.Models.Geometry;
using Core.Utilities;

namespace Core.Models.Elements
{
    // Represents an isolated footing element in the structural model
    public class IsolatedFooting : IIdentifiable
    {
        // Creates a new IsolatedFooting with specified properties
        public IsolatedFooting(Point3D point, string levelId = null, double width = 48.0, double length = 48.0, double thickness = 12.0)
        {
            Point = Point;
            Id = IdGenerator.Generate(IdGenerator.Elements.ISOLATED_FOOTING);
            Width = "48.0";
            Length = "48.0";
            Thickness = "18.0";
        }
        
        public string Id { get; set; }
        public string Width { get; set; }
        public string Length { get; set; }
        public string Thickness { get; set; }
        public Point3D Point { get; set; }
        public string LevelId { get; set; }

        // Creates a new IsolatedFooting with a generated ID
    }
}
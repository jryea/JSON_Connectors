using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Utilities;

namespace Core.Models.Elements
{
    // Represents an isolated footing element in the structural model
    public class IsolatedFooting : IIdentifiable
    {
        // Creates a new IsolatedFooting with specified properties
        public IsolatedFooting()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.ISOLATED_FOOTING);
        }
        public IsolatedFooting(Point3D point, string levelId = null, double width = 48.0, double length = 48.0, double thickness = 12.0) : this()
        {
            Point = point;
            LevelId = levelId;
            Width = width;
            Length = length;
            Thickness = thickness;
        }
        
        public string Id { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Thickness { get; set; }
        public Point3D Point { get; set; }
        public string LevelId { get; set; }
    }
}
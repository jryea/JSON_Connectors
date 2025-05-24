using Core.Utilities;
using Core.Models.Geometry;

namespace Core.Models.ModelLayout
{
    // Represents a level in the structural model
    public class Level : IIdentifiable, ITransformable
    {
        // Unique identifier for the level
        public string Id { get; set; }

        // Name of the level
        public string Name { get; set; }

        /// ID of the floor type associated with this level
        public string FloorTypeId { get; set; }

        /// Elevation or height of the level in model units
        public double Elevation { get; set; }

        // Creates a new Level with a generated ID
        public Level()
        {
            Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL);
        }

        // Creates a new Level with the specified properties
        public Level(string name, string floorTypeId, double elevation) : this()
        {
            Name = name;
            FloorTypeId = floorTypeId;
            Elevation = elevation;
        }
        

        // ITransformable implementation - rotates level in XY plane (no effect on elevation)
        public void Rotate(double angleDegrees, Point2D center)
        {
            // Levels don't have XY position, so rotation has no effect
            // This method is implemented for interface compliance
        }

        // ITransformable implementation - translates level elevation
        public void Translate(Point3D offset)
        {
            // Only the Z component affects level elevation
            Elevation += offset.Z;
        }
    }
}
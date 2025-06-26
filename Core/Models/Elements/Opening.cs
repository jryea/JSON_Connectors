using System.Collections.Generic;
using Core.Models.Geometry;
using Core.Utilities;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents an opening in a floor element
    /// </summary>
    public class Opening : IIdentifiable, ITransformable
    {
        /// <summary>
        /// Unique identifier for the opening
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// ID of the level this opening belongs to
        /// </summary>
        public string LevelId { get; set; }

        /// <summary>
        /// Collection of points defining the opening boundary
        /// </summary>
        public List<Point2D> Points { get; set; } = new List<Point2D>();

        /// <summary>
        /// Creates a new Opening with a generated ID
        /// </summary>
        public Opening()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.OPENING);
            Points = new List<Point2D>();
        }

        /// <summary>
        /// Creates a new Opening with specified properties
        /// </summary>
        public Opening(string levelId, List<Point2D> points) : this()
        {
            LevelId = levelId;
            Points = points ?? new List<Point2D>();
        }

        /// <summary>
        /// ITransformable implementation
        /// </summary>
        public void Rotate(double angleDegrees, Point2D center)
        {
            if (Points != null)
            {
                foreach (var point in Points)
                {
                    point.Rotate(angleDegrees, center);
                }
            }
        }

        public void Translate(Point3D offset)
        {
            if (Points != null)
            {
                foreach (var point in Points)
                {
                    point.Translate(offset);
                }
            }
        }
    }
}
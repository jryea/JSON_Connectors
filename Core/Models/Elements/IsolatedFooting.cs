using Core.Utilities;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents an isolated footing element in the structural model
    /// </summary>
    public class IsolatedFooting : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the isolated footing
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Point defining the location of the isolated footing
        /// </summary>
        public Point3D Point { get; set; }

        /// <summary>
        /// Creates a new IsolatedFooting with a generated ID
        /// </summary>
        public IsolatedFooting()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.ISOLATED_FOOTING);
        }

        /// <summary>
        /// Creates a new IsolatedFooting with specified properties
        /// </summary>
        /// <param name="point">Point defining the location of the isolated footing</param>
        public IsolatedFooting(Point3D point) : this()
        {
            Point = point;
        }
    }
}
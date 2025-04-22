using Core.Models.Geometry;
using Core.Models;
using Core.Utilities;

namespace Core.Models.Elements
{
    public class IsolatedFooting : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the isolated footing
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Width of the footing
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Length of the footing
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// Thickness of the footing
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// Location of the footing
        /// </summary>
        public Point3D Point { get; set; }

        /// <summary>
        /// ID of the level this footing belongs to
        /// </summary>
        public string LevelId { get; set; }

        /// <summary>
        /// Creates a new IsolatedFooting with a generated ID
        /// </summary>
        public IsolatedFooting()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.ISOLATED_FOOTING);
        }
    }
        
}

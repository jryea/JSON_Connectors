using System.Collections.Generic;
using Core.Utilities;

namespace Core.Models.Elements
{
    /// <summary>
    /// Represents a brace element in the structural model
    /// </summary>
    public class Brace : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the brace
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Material ID
        /// </summary>
        public string MaterialId { get; set; }

        /// <summary>
        /// Frame Section Id
        /// </summary>
        public string FramePropertiesId { get; set; }  

        /// <summary>
        /// BaseLevel ID
        /// </summary>
        public string BaseLevelId { get; set; }

        /// <summary>
        /// BaseLevel ID
        /// </summary>
        public string TopLevelId { get; set; }

        /// <summary>
        /// Start point
        /// </summary>
        public Point2D StartPoint { get; set; }

        /// <summary>
        /// End point
        /// </summary>
        public Point2D EndPoint { get; set; }

        /// <summary>
        /// Creates a new Brace with a generated ID
        /// </summary>
        public Brace()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.BRACE);
        }
    }
}
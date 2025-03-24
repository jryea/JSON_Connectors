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
        /// Section ID
        /// </summary>
        public Dictionary<string, string> SectionId { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Level ID
        /// </summary>
        public string LevelId { get; set; }

        /// <summary>
        /// Start point
        /// </summary>
        public Point3D StartPoint { get; set; }

        /// <summary>
        /// End point
        /// </summary>
        public Point3D EndPoint { get; set; }

        /// <summary>
        /// Creates a new Brace with a generated ID
        /// </summary>
        public Brace()
        {
            Id = IdGenerator.Generate(IdGenerator.Elements.BRACE);
            SectionId = new Dictionary<string, string>();
        }

        /// <summary>
        /// Creates a new Brace with specified properties
        /// </summary>
        /// <param name="materialId">Material ID</param>
        /// <param name="levelId">Level ID</param>
        /// <param name="startPoint">Start point</param>
        /// <param name="endPoint">End point</param>
        public Brace(string materialId, string levelId, Point3D startPoint, Point3D endPoint) : this()
        {
            MaterialId = materialId;
            LevelId = levelId;
            StartPoint = startPoint;
            EndPoint = endPoint;
        }
    }
}
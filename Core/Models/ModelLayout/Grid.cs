using Core.Models.Geometry;
using Core.Utilities;

namespace Core.Models.ModelLayout
{
    /// <summary>
    /// Represents a grid in the structural model
    /// </summary>
    public class Grid : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the grid
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the grid
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Starting point of the grid
        /// </summary>
        public GridPoint StartPoint { get; set; }

        /// <summary>
        /// Ending point of the grid
        /// </summary>
        public GridPoint EndPoint { get; set; }

        /// <summary>
        /// Creates a new Grid with a generated ID
        /// </summary>
        public Grid()
        {
            Id = IdGenerator.Generate(IdGenerator.Layout.GRID);
        }

        /// <summary>
        /// Creates a new Grid with the specified properties
        /// </summary>
        /// <param name="name">Name of the grid</param>
        /// <param name="startPoint">Starting point</param>
        /// <param name="endPoint">Ending point</param>
        public Grid(string name, GridPoint startPoint, GridPoint endPoint) : this()
        {
            Name = name;
            StartPoint = startPoint;
            EndPoint = endPoint;
        }
    }
}
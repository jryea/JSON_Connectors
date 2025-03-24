using Core.Utilities;

namespace Core.Models.ModelLayout
{
    /// <summary>
    /// Represents a floor type in the structural model
    /// </summary>
    public class FloorType : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the floor type
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the floor type
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the floor type
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Creates a new FloorType with a generated ID
        /// </summary>
        public FloorType()
        {
            Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE);
        }

        /// <summary>
        /// Creates a new FloorType with the specified properties
        /// </summary>
        /// <param name="name">Name of the floor type</param>
        /// <param name="description">Description of the floor type</param>
        public FloorType(string name, string description = null) : this()
        {
            Name = name;
            Description = description;
        }
    }
}
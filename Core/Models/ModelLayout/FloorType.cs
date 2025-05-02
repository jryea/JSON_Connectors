using Core.Utilities;

namespace Core.Models.ModelLayout
{
    // Represents a floor type in the structural model
    public class FloorType : IIdentifiable
    {
        // Unique identifier for the floor type
        public string Id { get; set; }

        // Name of the floor type
        public string Name { get; set; }

        // Creates a new FloorType with a generated ID
        public FloorType()
        {
            Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE);
        }

        // Creates a new FloorType with the specified properties
        public FloorType(string name) : this()
        {
            Name = name;
        }
    }
}
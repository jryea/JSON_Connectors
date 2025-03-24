using Core.Utilities;

namespace Core.Models.ModelLayout
{
    /// <summary>
    /// Represents a level in the structural model
    /// </summary>
    public class Level : IIdentifiable
    {
        /// <summary>
        /// Unique identifier for the level
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the level
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// ID of the floor type associated with this level
        /// </summary>
        public string FloorTypeId { get; set; }

        /// <summary>
        /// Elevation or height of the level in model units
        /// </summary>
        public double ElevationOrHeight { get; set; }

        /// <summary>
        /// Creates a new Level with a generated ID
        /// </summary>
        public Level()
        {
            Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL);
        }

        /// <summary>
        /// Creates a new Level with the specified properties
        /// </summary>
        /// <param name="name">Name of the level</param>
        /// <param name="floorTypeId">Floor type ID</param>
        /// <param name="elevationOrHeight">Elevation or height</param>
        public Level(string name, string floorTypeId, double elevationOrHeight) : this()
        {
            Name = name;
            FloorTypeId = floorTypeId;
            ElevationOrHeight = elevationOrHeight;
        }
    }
}
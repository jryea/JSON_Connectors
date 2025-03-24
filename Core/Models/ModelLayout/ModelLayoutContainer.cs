using System.Collections.Generic;

namespace Core.Models.ModelLayout
{
    /// <summary>
    /// Container for model layout components (grids, levels, floor types)
    /// </summary>
    public class ModelLayoutContainer
    {
        /// <summary>
        /// Collection of grids
        /// </summary>
        public List<Grid> Grids { get; set; } = new List<Grid>();

        /// <summary>
        /// Collection of levels
        /// </summary>
        public List<Level> Levels { get; set; } = new List<Level>();

        /// <summary>
        /// Collection of floor types
        /// </summary>
        public List<FloorType> FloorTypes { get; set; } = new List<FloorType>();

        /// <summary>
        /// Creates a new ModelLayoutContainer with empty collections
        /// </summary>
        public ModelLayoutContainer()
        {
            Grids = new List<Grid>();
            Levels = new List<Level>();
            FloorTypes = new List<FloorType>();
        }
    }
}
using System.Collections.Generic;

namespace Core.Models.ModelLayout
{
    // Container for model layout components (grids, levels, floor types)
    public class ModelLayoutContainer
    {
        // Collection of grids
        public List<Grid> Grids { get; set; } = new List<Grid>();

        // Collection of levels
        public List<Level> Levels { get; set; } = new List<Level>();

        // Collection of floor types
        public List<FloorType> FloorTypes { get; set; } = new List<FloorType>();

        // Creates a new ModelLayoutContainer with empty collections
        public ModelLayoutContainer()
        {
            Grids = new List<Grid>();
            Levels = new List<Level>();
            FloorTypes = new List<FloorType>();
        }
    }
}
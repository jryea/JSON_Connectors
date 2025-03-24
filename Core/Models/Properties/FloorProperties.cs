using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Properties
{
    /// <summary>
    /// Represents properties for floor elements in the structural model
    /// </summary>
    public class FloorProperties
    {
        /// <summary>
        /// Name of the floor property
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Type of floor (e.g., "Slab", "Composite", "NonComposite")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Thickness of the floor in model units (typically inches)
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// ID of the material for this floor
        /// </summary>
        public Material Material { get; set; }

        /// <summary>
        /// Additional slab-specific properties (when Type is slab)
        /// </summary>
        public Dictionary<string, object> SlabProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Additional deck-specific properties (when Type is deck)
        /// </summary>
        public Dictionary<string, object> DeckProperties { get; set; } = new Dictionary<string, object>();
        public string Reinforcement { get; set; }
    }
}
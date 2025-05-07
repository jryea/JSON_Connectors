namespace Core.Models.Properties.Floors
{
    /// <summary>
    /// Properties specific to concrete slab floors
    /// </summary>
    public class SlabProperties : FloorTypeProperties
    {
        /// <summary>
        /// Indicates if the slab has ribs
        /// </summary>
        public bool IsRibbed { get; set; }

        /// <summary>
        /// Indicates if the slab is a waffle slab
        /// </summary>
        public bool IsWaffle { get; set; }

        /// <summary>
        /// Indicates if the slab spans in two directions
        /// </summary>
        public bool IsTwoWay { get; set; }

        /// <summary>
        /// Reinforcement specification
        /// </summary>
        public string Reinforcement { get; set; }

        /// <summary>
        /// Creates a new SlabProperties with default values
        /// </summary>
        public SlabProperties()
        {
            IsRibbed = false;
            IsWaffle = false;
            IsTwoWay = true;
            Reinforcement = "Default";
        }
    }
}
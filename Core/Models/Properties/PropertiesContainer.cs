using Core.Models.Properties.Floors;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Properties
{
    /// <summary>
    /// Container for all property definitions in the structural model
    /// </summary>
    public class PropertiesContainer
    {
        /// <summary>
        /// Collection of material definitions
        /// </summary>
        public List<Material> Materials { get; set; } = new List<Material>();

        /// <summary>
        /// Collection of wall property definitions
        /// </summary>
        public List<WallProperties> WallProperties { get; set; } = new List<WallProperties>();

        /// <summary>
        /// Collection of floor property definitions
        /// </summary>
        public List<FloorProperties> FloorProperties { get; set; } = new List<FloorProperties>();

        /// <summary>
        /// Collection of diaphragm definitions
        /// </summary>
        public List<Diaphragm> Diaphragms { get; set; } = new List<Diaphragm>();

        /// <summary>
        /// Collection of pier/spandrel definitions
        /// </summary>
   
        public List<FrameProperties> FrameProperties { get; set; } = new List<FrameProperties>();
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Properties
{
    // Container for all property definitions in the structural model
    public class PropertiesContainer
    {
        // Collection of material definitions
        public List<Material> Materials { get; set; } = new List<Material>();

        // Collection of wall property definitions
        public List<WallProperties> WallProperties { get; set; } = new List<WallProperties>();

        // Collection of floor property definitions
        public List<FloorProperties> FloorProperties { get; set; } = new List<FloorProperties>();

        // Collection of diaphragm definitions
        public List<Diaphragm> Diaphragms { get; set; } = new List<Diaphragm>();

        // Collection of pier/spandrel definitions
        public List<FrameProperties> FrameProperties { get; set; } = new List<FrameProperties>();
    }
}
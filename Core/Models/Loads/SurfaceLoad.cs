using Core.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Loads
{
    // Represents a surface load in RAM and a Shell Uniform Load Set in ETABS
 
    public class SurfaceLoad
    {
        public string Id { get; set; }
        public string LayoutTypeId { get; set; }
        public string LiveLoadId { get; set; }
        public double LiveLoadValue { get; set; }
        public string DeadLoadId { get; set; }
        public double DeadLoadValue { get; set; }

        public SurfaceLoad()
        {
            Id = IdGenerator.Generate(IdGenerator.Loads.SURFACE_LOAD);
        }
    }
}

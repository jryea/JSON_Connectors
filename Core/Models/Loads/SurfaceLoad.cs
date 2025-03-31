using Core.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Loads
{
    /// <summary>
    /// Represents a surface load in the structural model
    /// </summary>
    public class SurfaceLoad
    {
        public string Id { get; set; }
        public string LayoutTypeId { get; set; }
        public string LiveLoadId { get; set; }
        public string DeadLoadId { get; set; }

        public SurfaceLoad()
        {
            Id = IdGenerator.Generate(IdGenerator.Loads.SURFACE_LOAD);  
        }
    }
}

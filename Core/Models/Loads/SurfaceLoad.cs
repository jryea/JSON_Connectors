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
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string LayoutTypeId { get; set; }
        public string DeadId { get; set; }
        public string LiveId { get; set; }
    }
}

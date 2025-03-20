using Core.Models.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Model
{
    /// <summary>
    /// Container for model
    /// </summary>
    public class StructuralModel
    {
        public List<Grid> Grids { get; set; } = new List<Grid>();
        public List<Level> Levels { get; set; } = new List<Level>();
        public List<FloorType> FloorTypes { get; set; } = new List<FloorType>();
    }
}

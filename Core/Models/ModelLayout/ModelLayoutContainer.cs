using Core.Models.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.ModelLayout
{
    /// <summary>
    /// Container for model
    /// </summary>
    public class ModelLayoutContainer
    {
        public List<Grid> Grids { get; set; } = new List<Grid>();
        public List<Level> Levels { get; set; } = new List<Level>();
        public List<FloorType> FloorTypes { get; set; } = new List<FloorType>();
    }
}

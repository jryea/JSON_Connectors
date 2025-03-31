using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Loads
{
    // Container for all loads in the structural model
   
    public class LoadContainer
    {
        public List<LoadDefinition> LoadDefinitions { get; set; } = new List<LoadDefinition>();
        public List<SurfaceLoad> SurfaceLoads { get; set; } = new List<SurfaceLoad>();
        public List<LoadCombination> LoadCombinations { get; set; } = new List<LoadCombination>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Loads
{
    /// </summary>
    /// <summary>
    /// Represents a load definition in the structural model
    /// </summary>
    public class LoadDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; }
        public string Name { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

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

    /// <summary>
    /// Represents a load combination in the structural model
    /// </summary>
    public class LoadCombination
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string LoadDefinitionId { get; set; }
    }

    /// <summary>
    /// Container for all loads in the structural model
    /// </summary>
    public class LoadContainer
    {
        public List<LoadDefinition> LoadDefinitions { get; set; } = new List<LoadDefinition>();
        public List<SurfaceLoad> SurfaceLoads { get; set; } = new List<SurfaceLoad>();
        public List<LoadCombination> LoadCombinations { get; set; } = new List<LoadCombination>();
    }
}

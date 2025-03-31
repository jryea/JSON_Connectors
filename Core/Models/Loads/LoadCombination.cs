using Core.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Loads
{
    /// <summary>
    /// Represents a load combination in the structural model
    /// </summary>
    public class LoadCombination
    {
        public string Id { get; set; }
        public List<string> LoadDefinitionIds { get; set; }
     public LoadCombination()
        {
            Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_COMBINATION);
        }
    }
}

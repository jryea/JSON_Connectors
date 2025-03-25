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
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string LoadDefinitionId { get; set; }
    }
}

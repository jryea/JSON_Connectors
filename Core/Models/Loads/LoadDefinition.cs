using Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Loads
{
    /// <summary>
    /// Represents a load definition in the structural model
    /// </summary>
    public class LoadDefinition
    {
        public string Id { get; set; }
        public string Type { get; set; }  
        public string Name { get; set; }
        public double SelfWeight { get; set; }
        public LoadDefinition() 
        {
            Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION);
        }
    }
}

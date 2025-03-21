using Core.Models.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Metadata
{
    /// <summary>
    /// Container for model
    /// </summary>
    public class MetadataContainer  
    {
        public ProjectInfo ProjectInfo { get; set; } = new ProjectInfo();
        public Units Units { get; set; } = new Units(); 
    }
}

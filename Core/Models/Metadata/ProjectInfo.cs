using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Metadata
{
    public class ProjectInfo
    {
        public string ProjectName { get; set; }
        public DateTime CreationDate { get; set; }
        public string SchemaVersion { get; set; }
    }
}
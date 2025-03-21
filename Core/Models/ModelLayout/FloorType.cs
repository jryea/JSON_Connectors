using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.ModelLayout
{
    public class FloorType
    {
        public string Name { get; set; }
        public Guid Id { get; private set; }

        public FloorType(string name)
        {
            Name = name;
            Id = Guid.NewGuid();
        }
       
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Model
{
    public class FloorType
    {
        public string Name { get; set; }

        public FloorType(string name)
        {
            Name = name;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAM.ToRAM
{
    public interface IModelToRAM<T>
    {
        T Import();
    }
}

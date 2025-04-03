// IRAMExporter.cs
using Core.Models;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public interface IRAMExporter
    {
        void Export(BaseModel model);
    }
}
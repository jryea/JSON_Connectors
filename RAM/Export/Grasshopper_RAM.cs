using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;
using RAMDATAACCESSLib;

namespace JSON_Connectors.Connectors.RAM.Export
{
    public class Grasshopper_RAM : GH_AssemblyInfo
    {
        public override string Name => "Grasshopper_RAM_Test2";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("80db5f72-7924-4660-b91d-7c13c706eb6c");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";

        //Return a string representing the version.  This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}
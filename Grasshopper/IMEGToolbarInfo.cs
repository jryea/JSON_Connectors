using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace IMEGToolbarInfo
{
    public class IMEGToolbarInfo : GH_AssemblyInfo
    {
        public override string Name => "IMEG Toolbar";

        public override string Version => "1.0.0";

        public override Bitmap Icon => null;

        public override string Description => "IMEG tools";

        public override Guid Id => new Guid("80D76120-2908-4CE5-BDEA-1A24A47F4FDF");

        public override string AuthorName => "IMEG";
    }
}
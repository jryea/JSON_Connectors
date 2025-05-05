using Rhino.PlugIns;
using System.Runtime.InteropServices;

namespace StructuralSetup
{
    [Guid("a2c89d68-fc45-4b8b-af84-f056c41a79a7")]
    public class StructuralSetupPlugin : PlugIn
    {
        public StructuralSetupPlugin()
        {
            Instance = this;
        }

        public static StructuralSetupPlugin Instance { get; private set; }
    }
}
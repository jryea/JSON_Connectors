using System.Drawing;
using System.IO;
using System.Reflection;

namespace Grasshopper.Utilities
{
    public static class Helpers
    {
        public static Bitmap GetLogo()
        {
            // Get the current assembly
            Assembly assembly = Assembly.GetExecutingAssembly();

            // Load the image from the embedded resource
            using (Stream stream = assembly.GetManifestResourceStream("Grasshopper.Resources.IMEG_Logo_Grasshopper.png"))
            {
                if (stream == null)
                    return null;

                return new Bitmap(stream);
            }
        }
    }
}
using Grasshopper.Kernel;
using System.Drawing;
using Grasshopper.Utilities;
using System.Runtime.CompilerServices;

namespace Grasshopper.Components.Core
{
    public abstract class ComponentBase : GH_Component
    {
        protected ComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
        }

        // Override the Icon property to use your custom logo
        protected override Bitmap Icon => Helpers.GetLogo();

        // Optionally override other common properties or methods for all your components
    }
}
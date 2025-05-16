using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using static Core.Models.Properties.Modifiers;
using Grasshopper.Components.Core;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.ETABS
{
    public class ETABSShellModifiersComponent : ComponentBase
    {
        public ETABSShellModifiersComponent()
          : base("ETABS Shell Modifiers", "EShellMod",
              "Creates ETABS-specific modifiers for shell elements (floors, walls)",
              "IMEG", "ETABS")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("F11", "F11", "Membrane stiffness modifier in local 1-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("F22", "F22", "Membrane stiffness modifier in local 2-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("F12", "F12", "In-plane shear stiffness modifier", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("M11", "M11", "Bending stiffness modifier in local 1-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("M22", "M22", "Bending stiffness modifier in local 2-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("M12", "M12", "Twisting stiffness modifier", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("V13", "V13", "Out-of-plane shear modifier in local 1-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("V23", "V23", "Out-of-plane shear modifier in local 2-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Mass", "M", "Mass modifier", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Weight", "W", "Weight modifier", GH_ParamAccess.item, 1.0);

            // Make all parameters optional
            for (int i = 0; i < 10; i++)
                pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Shell Modifiers", "SM", "ETABS-specific shell modifiers", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double f11 = 1.0;
            double f22 = 1.0;
            double f12 = 1.0;
            double m11 = 1.0;
            double m22 = 1.0;
            double m12 = 1.0;
            double v13 = 1.0;
            double v23 = 1.0;
            double mass = 1.0;
            double weight = 1.0;

            // Get all input values (using default if not provided)
            DA.GetData(0, ref f11);
            DA.GetData(1, ref f22);
            DA.GetData(2, ref f12);
            DA.GetData(3, ref m11);
            DA.GetData(4, ref m22);
            DA.GetData(5, ref m12);
            DA.GetData(6, ref v13);
            DA.GetData(7, ref v23);
            DA.GetData(8, ref mass);
            DA.GetData(9, ref weight);

            // Create modifiers
            ETABSShellModifiers modifiers = new ETABSShellModifiers
            {
                F11 = f11,
                F22 = f22,
                F12 = f12,
                M11 = m11,
                M22 = m22,
                M12 = m12,
                V13 = v13,
                V23 = v23,
                Mass = mass,
                Weight = weight
            };

            // Output
            DA.SetData(0, new GH_ETABSShellModifiers(modifiers));
        }

        public override Guid ComponentGuid => new Guid("B2C3D4E5-F6A7-8901-2B3C-4D5E6F7A8B9C");
    }
}
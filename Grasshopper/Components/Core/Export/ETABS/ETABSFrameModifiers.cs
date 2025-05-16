using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using static Core.Models.Properties.Modifiers;
using Grasshopper.Components.Core;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.ETABS
{
    public class ETABSFrameModifiersComponent : ComponentBase
    {
        public ETABSFrameModifiersComponent()
          : base("ETABS Frame Modifiers", "EFrameMod",
              "Creates ETABS-specific modifiers for frame elements (beams, columns, braces)",
              "IMEG", "ETABS")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Area", "A", "Area modifier", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Shear Area 2", "A2", "Shear area modifier in local 2-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Shear Area 3", "A3", "Shear area modifier in local 3-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Inertia 2", "I2", "Moment of inertia modifier in local 2-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Inertia 3", "I3", "Moment of inertia modifier in local 3-direction", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Torsion", "J", "Torsional constant modifier", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Mass", "M", "Mass modifier", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Weight", "W", "Weight modifier", GH_ParamAccess.item, 1.0);

            // Make all parameters optional
            for (int i = 0; i < 8; i++)
                pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Frame Modifiers", "FM", "ETABS-specific frame modifiers", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double area = 1.0;
            double a22 = 1.0;
            double a33 = 1.0;
            double i22 = 1.0;
            double i33 = 1.0;
            double torsion = 1.0;
            double mass = 1.0;
            double weight = 1.0;

            // Get all input values (using default if not provided)
            DA.GetData(0, ref area);
            DA.GetData(1, ref a22);
            DA.GetData(2, ref a33);
            DA.GetData(3, ref i22);
            DA.GetData(4, ref i33);
            DA.GetData(5, ref torsion);
            DA.GetData(6, ref mass);
            DA.GetData(7, ref weight);

            // Create modifiers
            ETABSFrameModifiers modifiers = new ETABSFrameModifiers
            {
                Area = area,
                A22 = a22,
                A33 = a33,
                I22 = i22,
                I33 = i33,
                Torsion = torsion,
                Mass = mass,
                Weight = weight
            };

            // Output
            DA.SetData(0, new GH_ETABSFrameModifiers(modifiers));
        }

        public override Guid ComponentGuid => new Guid("A1B2C3D4-E5F6-7890-1A2B-3C4D5E6F7A8B");
    }
}
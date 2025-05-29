using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class SteelPropertiesComponent : ComponentBase
    {
        public SteelPropertiesComponent()
          : base("Steel Material Properties", "SteelProps",
              "Creates steel-specific material properties",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Fy", "Fy", "Yield strength (psi)", GH_ParamAccess.item, 50000.0);
            pManager.AddNumberParameter("Fu", "Fu", "Ultimate tensile strength (psi)", GH_ParamAccess.item, 65000.0);
            pManager.AddNumberParameter("Fye", "Fye", "Expected yield strength (psi)", GH_ParamAccess.item, 55000.0);
            pManager.AddNumberParameter("Fue", "Fue", "Expected ultimate strength (psi)", GH_ParamAccess.item, 71500.0);

            // Make all parameters optional
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Steel Properties", "SP", "Steel-specific material properties", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double fy = 0.0;
            double fu = 0.0;
            double fye = 0.0;
            double fue = 0.0;

            DA.GetData(0, ref fy);
            DA.GetData(1, ref fu);
            DA.GetData(2, ref fye);
            DA.GetData(3, ref fue);

            // Create the steel properties - let defaults come from core
            SteelProperties steelProps = new SteelProperties();

            // Override properties only if positive values are provided
            if (fy > 0)
                steelProps.Fy = fy;

            if (fu > 0)
                steelProps.Fu = fu;

            if (fye > 0)
                steelProps.Fye = fye;

            if (fue > 0)
                steelProps.Fue = fue;

            // Output the steel properties
            DA.SetData(0, new GH_SteelProperties(steelProps));
        }
        public override Guid ComponentGuid => new Guid("F7E6D5C4-B3A2-1098-7F6E-5D4C3B2A1098");
    }
}
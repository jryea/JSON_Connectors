using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Core.Models;
using Core.Models.Properties;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class ConcretePropertiesComponent : ComponentBase
    {
        public ConcretePropertiesComponent()
          : base("Concrete Material Properties", "ConcMatProps",
              "Creates concrete-specific material properties",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("f'c", "f'c", "Compressive strength (psi)", GH_ParamAccess.item, 4000.0);
            pManager.AddTextParameter("Weight Class", "WC", "Weight class (Normal or Lightweight)", GH_ParamAccess.item, "Normal");
            pManager.AddNumberParameter("Shear Strength Reduction", "SSR", "Shear strength reduction factor", GH_ParamAccess.item, 1.0);

            // Make all parameters optional
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Concrete Properties", "CP", "Concrete-specific material properties", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double fc = 0.0;
            string weightClassStr = "Normal";
            double shearReduction = 0.0;

            DA.GetData(0, ref fc);
            DA.GetData(1, ref weightClassStr);
            DA.GetData(2, ref shearReduction);

            // Parse weight class
            WeightClass weightClass;
            if (!Enum.TryParse(weightClassStr, true, out weightClass))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Unknown weight class: {weightClassStr}, defaulting to Normal");
                weightClass = WeightClass.Normal;
            }

            // Create the concrete properties
            ConcreteProperties concreteProps = new ConcreteProperties();

            // Override default values only if provided
            if (fc > 0)
                concreteProps.Fc = fc;

            concreteProps.WeightClass = weightClass;

            if (shearReduction > 0)
                concreteProps.ShearStrengthReductionFactor = shearReduction;

            // Output the concrete properties
            DA.SetData(0, new GH_ConcreteProperties(concreteProps));
        }

        public override Guid ComponentGuid => new Guid("D1C2B3A4-E5F6-7890-A1B2-C3D4E5F6A7B8");
    }
}
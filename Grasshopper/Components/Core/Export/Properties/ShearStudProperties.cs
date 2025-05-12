using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class ShearStudPropertiesComponent : ComponentBase
    {
        public ShearStudPropertiesComponent()
          : base("Shear Stud Properties", "StudProps",
              "Creates shear stud properties for composite floor systems",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Diameter", "D", "Shear stud diameter (in inches)", GH_ParamAccess.item, 0.75);
            pManager.AddNumberParameter("Height", "H", "Shear stud height (in inches)", GH_ParamAccess.item, 6.0);
            pManager.AddNumberParameter("Tensile Strength", "Fu", "Shear stud tensile strength (in psi)", GH_ParamAccess.item, 65000.0);

            // Make all parameters optional
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Shear Stud Properties", "SP", "Shear stud properties for composite floors", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double diameter = 0.0;
            double height = 0.0;
            double tensileStrength = 0.0;

            DA.GetData(0, ref diameter);
            DA.GetData(1, ref height);
            DA.GetData(2, ref tensileStrength);

            // Create the shear stud properties
            ShearStudProperties studProps = new ShearStudProperties();

            // Only override defaults if positive values are provided
            if (diameter > 0)
                studProps.ShearStudDiameter = diameter;

            if (height > 0)
                studProps.ShearStudHeight = height;

            if (tensileStrength > 0)
                studProps.ShearStudTensileStrength = tensileStrength;

            // Output the shear stud properties
            DA.SetData(0, new GH_ShearStudProperties(studProps));
        }

        public override Guid ComponentGuid => new Guid("C2D3E4F5-6A7B-8901-2345-6789ABCDEF01");
    }
}
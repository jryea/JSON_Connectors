using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Metadata;

namespace Grasshopper.Components.Core.Export.Metadata
{
    public class UnitsComponent : ComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the Units component.
        /// </summary>
        public UnitsComponent()
          : base("Units", "Units",
              "Creates a units definition for the structural model",
              "IMEG", "Metadata")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Length Unit", "L", "Length unit (e.g., 'inches', 'feet', 'mm', 'm')", GH_ParamAccess.item, "inches");
            pManager.AddTextParameter("Force Unit", "F", "Force unit (e.g., 'pounds', 'kips', 'N', 'kN')", GH_ParamAccess.item, "pounds");
            pManager.AddTextParameter("Temperature Unit", "T", "Temperature unit (e.g., 'fahrenheit', 'celsius')", GH_ParamAccess.item, "fahrenheit");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Units", "U", "Units definition for the structural model", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            string lengthUnit = "inches";
            string forceUnit = "pounds";
            string temperatureUnit = "fahrenheit";

            DA.GetData(0, ref lengthUnit);
            DA.GetData(1, ref forceUnit);
            DA.GetData(2, ref temperatureUnit);

            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(lengthUnit))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Length unit is empty, using default 'inches'");
                    lengthUnit = "inches";
                }

                if (string.IsNullOrWhiteSpace(forceUnit))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Force unit is empty, using default 'pounds'");
                    forceUnit = "pounds";
                }

                if (string.IsNullOrWhiteSpace(temperatureUnit))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Temperature unit is empty, using default 'fahrenheit'");
                    temperatureUnit = "fahrenheit";
                }

                // Create units object
                Units units = new Units
                {
                    Length = lengthUnit,
                    Force = forceUnit,
                    Temperature = temperatureUnit
                };

                // Set output
                DA.SetData(0, units);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("8D1F2E3C-4B5A-6D7C-8E9F-0A1B2C3D4E5F");
    }
}
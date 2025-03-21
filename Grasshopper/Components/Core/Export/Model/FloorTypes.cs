using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Model;

namespace Grasshopper.Export
{
    public class FloorTypeCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FloorTypeCollector class.
        /// </summary>
        public FloorTypeCollectorComponent()
          : base("Floor Types", "FloorTypes",
              "Creates floor type objects that can be used in the structural model",
              "IMEG", "Model")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each floor type", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Floor Types", "FT", "Floor Type objects for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<string> names = new List<string>();

            if (!DA.GetDataList(0, names)) return;

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No floor type names provided");
                return;
            }

            try
            {
                // Create floor types
                List<FloorType> floorTypes = new List<FloorType>();

                foreach (string name in names)
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Empty floor type name skipped");
                        continue;
                    }

                    // Create a new floor type
                    FloorType floorType = new FloorType(name);
                    floorTypes.Add(floorType);
                }

                // Set output
                DA.SetDataList(0, floorTypes);
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
        public override Guid ComponentGuid => new Guid("1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D");
    }
}
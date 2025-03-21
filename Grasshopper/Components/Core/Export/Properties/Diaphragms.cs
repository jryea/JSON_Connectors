using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;

namespace Grasshopper.Export
{
    public class DiaphragmCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DiaphragmCollector class.
        /// </summary>
        public DiaphragmCollectorComponent()
          : base("Diaphragms", "Diaphragms",
              "Creates diaphragm definitions for the structural model",
              "IMEG", "Properties")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each diaphragm", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Types for each diaphragm (e.g., 'Rigid', 'Semi-Rigid', 'Flexible')", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Diaphragms", "D", "Diaphragm definitions for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<string> names = new List<string>();
            List<string> types = new List<string>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, types)) return;

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No diaphragm names provided");
                return;
            }

            if (names.Count != types.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of names ({names.Count}) must match number of types ({types.Count})");
                return;
            }

            try
            {
                // Create diaphragms
                List<Diaphragm> diaphragms = new List<Diaphragm>();

                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    string type = types[i];

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Empty diaphragm name or type skipped");
                        continue;
                    }

                    // Validate diaphragm type
                    if (!type.Equals("Rigid", StringComparison.OrdinalIgnoreCase) &&
                        !type.Equals("Semi-Rigid", StringComparison.OrdinalIgnoreCase) &&
                        !type.Equals("Flexible", StringComparison.OrdinalIgnoreCase))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Unrecognized diaphragm type '{type}' for diaphragm '{name}'. Using it anyway, but recommended types are 'Rigid', 'Semi-Rigid', or 'Flexible'.");
                    }

                    // Create a new diaphragm
                    Diaphragm diaphragm = new Diaphragm
                    {
                        Name = name,
                        Type = type
                    };

                    diaphragms.Add(diaphragm);
                }

                // Set output
                DA.SetDataList(0, diaphragms);
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
        public override Guid ComponentGuid => new Guid("9E8D7C6B-5A4F-3E2D-1C0B-9A8F7E6D5C4B");
    }
}
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using Grasshopper.Utilities;
using Core.Models.Properties;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class DiaphragmCollectorComponent : ComponentBase
    {
        public DiaphragmCollectorComponent()
          : base("Diaphragms", "Diaphragms",
              "Creates diaphragm definitions for the structural model",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each diaphragm", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Types for each diaphragm (e.g., 'Rigid', 'Semi-Rigid', 'Flexible')", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Diaphragms", "D", "Diaphragm definitions for the structural model", GH_ParamAccess.list);
        }

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
                List<GH_Diaphragm> diaphragms = new List<GH_Diaphragm>();

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
                    Diaphragm diaphragm = new Diaphragm()
                    {
                        Name = name,
                    };

                    diaphragms.Add(new GH_Diaphragm(diaphragm));
                }

                // Set output
                DA.SetDataList(0, diaphragms);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        public override Guid ComponentGuid => new Guid("9E8D7C6B-5A4F-3E2D-1C0B-9A8F7E6D5C4B");
    }
}
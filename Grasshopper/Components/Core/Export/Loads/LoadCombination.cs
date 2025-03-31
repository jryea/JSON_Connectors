using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using CM = Core.Models.Loads;
using Grasshopper.Components.Core;
using Grasshopper.Utilities;
using System.Drawing;

namespace Grasshopper.Components
{
    public class LoadCombinationComponent : ComponentBase
    {
        // Initializes a new instance of the LoadCombinationComponent class.
        public LoadCombinationComponent()
          : base("Load Combination", "LoadCombination",
              "Creates a load combination",
              "IMEG", "Loads")
        {
        }

        // Registers all the input parameters for this component.
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Load Definitions", "LDs", "List of load definition objects", GH_ParamAccess.list);
        }

        // Registers all the output parameters for this component.
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Load Combination", "LC", "Load combination", GH_ParamAccess.item);
        }

        // This is the method that actually does the work.
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize variables
            List<GH_LoadDefinition> ghLoadDefinitions = new List<GH_LoadDefinition>();

            // Retrieve input data
            if (!DA.GetDataList(0, ghLoadDefinitions)) return;

            // Create the load combination
            CM.LoadCombination loadCombo = new CM.LoadCombination
            {
                LoadDefinitionIds = new List<string>()
            };

            foreach (var ghLoadDef in ghLoadDefinitions)
            {
                if (ghLoadDef.Value != null)
                {
                    loadCombo.LoadDefinitionIds.Add(ghLoadDef.Value.Id);
                }
            }

            // Output the load combination wrapped in a Goo object
            DA.SetData(0, new GH_LoadCombination(loadCombo));
        }

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid
        {
            get { return new Guid("A1B2C3D4-E5F6-7890-1234-56789ABCDEF0"); }
        }
    }
}

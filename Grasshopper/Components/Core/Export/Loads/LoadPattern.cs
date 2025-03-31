using Core.Models.Loads;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grasshopper.Components.Core.Export.Loads
{
    /// <summary>
    /// Component for creating LoadDefinition (LoadPattern in ETABS terminology)
    /// </summary>
    public class LoadPatternComponent : GH_Component
    {
        public LoadPatternComponent()
            : base("Load Pattern", "LPattern",
                "Create a load pattern (load definition)",
                "Structural", "Loads")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name of the load pattern", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Type of load (Dead, Live, Wind, Seismic, etc.)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Self Weight", "SW", "Self weight multiplier (1 for Dead, 0 for others)", GH_ParamAccess.item, 0.0);
            pManager.AddGenericParameter("Properties", "P", "Additional properties (optional)", GH_ParamAccess.item);

            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("LoadPattern", "LP", "Load pattern definition", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Input parameters
            string name = string.Empty;
            string type = string.Empty;
            double selfWeight = 0.0;
            Dictionary<string, object> properties = new Dictionary<string, object>();

            // Get inputs
            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetData(1, ref type)) return;
            DA.GetData(2, ref selfWeight);

            // Try to get properties dictionary
            object propsObj = null;
            if (DA.GetData(3, ref propsObj) && propsObj != null)
            {
                // Try to cast to dictionary
                if (propsObj is Dictionary<string, object> dictProps)
                {
                    properties = dictProps;
                }
                else if (propsObj is IDictionary<string, object> idictProps)
                {
                    foreach (var kvp in idictProps)
                    {
                        properties[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Create the load definition
            LoadDefinition loadDef = new LoadDefinition
            {
                Id = Core.Utilities.IdGenerator.Generate(Core.Utilities.IdGenerator.Loads.LOAD_DEFINITION),
                Name = name,
                Type = type,
                Properties = properties
            };

            // Add self weight property
            loadDef.Properties["selfWeight"] = selfWeight;

            // Output
            DA.SetData(0, loadDef);
        }

        public override Guid ComponentGuid => new Guid("581A3C15-F95E-49F4-B632-A5F20B8E0E33");

        protected override Bitmap Icon => null;
    }
}

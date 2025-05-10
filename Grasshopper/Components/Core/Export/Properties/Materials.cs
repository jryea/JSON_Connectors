// Grasshopper/Components/Core/Export/Properties/Materials.cs
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class MaterialCollectorComponent : ComponentBase
    {
        public MaterialCollectorComponent()
          : base("Materials", "Materials",
              "Creates material definitions for the structural model",
              "IMEG", "Properties")
        {
        }
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each material", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Material types (Concrete, Steel, etc.)", GH_ParamAccess.list);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Materials", "M", "Material definitions", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> names = new List<string>();
            List<string> types = new List<string>();

            if (!DA.GetDataList(0, names)) return;
            DA.GetDataList(1, types);

            // Provide default type if needed
            if (types.Count == 0)
            {
                types = new List<string>(new string[names.Count]);
                for (int i = 0; i < names.Count; i++)
                    types[i] = "Concrete";
            }

            if (names.Count != types.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of names must match number of types");
                return;
            }

            List<GH_Material> materials = new List<GH_Material>();
            for (int i = 0; i < names.Count; i++)
            {
                // Parse material type
                MaterialType materialType;
                if (!Enum.TryParse(types[i], true, out materialType))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unknown material type: {types[i]}, defaulting to Concrete");
                    materialType = MaterialType.Concrete;
                }

                Material material = new Material(names[i], materialType);

                // Material properties are now initialized in the constructor
                materials.Add(new GH_Material(material));
            }

            DA.SetDataList(0, materials);
        }

        public override Guid ComponentGuid => new Guid("E8F2A9B3-D67C-45F1-BA8E-C95D30A2B714");
    }
}

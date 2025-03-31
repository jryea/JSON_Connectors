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
            pManager.AddTextParameter("Reinforcing", "R", "Reinforcing types (optional)", GH_ParamAccess.list);

            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Materials", "M", "Material definitions", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> names = new List<string>();
            List<string> types = new List<string>();
            List<string> reinforcingTypes = new List<string>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, types)) return;
            DA.GetDataList(2, reinforcingTypes);

            if (names.Count != types.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of names must match number of types");
                return;
            }

            List<GH_Material> materials = new List<GH_Material>();
            for (int i = 0; i < names.Count; i++)
            {
                Material material = new Material
                {
                    Name = names[i],
                    Type = types[i],
                    Reinforcing = reinforcingTypes.Count > i ? reinforcingTypes[i] : null
                };

                // Add default design data based on material type
                AddDefaultDesignData(material);

                materials.Add(new GH_Material(material));
            }

            DA.SetDataList(0, materials);
        }

        private void AddDefaultDesignData(Material material)
        {
            switch (material.Type.ToLower())
            {
                case "concrete":
                    material.DesignData["fc"] = 4000.0; // psi
                    material.DesignData["densityPCF"] = 150.0; // pcf
                    break;
                case "steel":
                    material.DesignData["fy"] = 50000.0; // psi
                    material.DesignData["fu"] = 65000.0; // psi
                    material.DesignData["E"] = 29000000.0; // psi
                    break;
                case "wood":
                    material.DesignData["fb"] = 1000.0; // psi
                    material.DesignData["E"] = 1600000.0; // psi
                    break;
            }
        }

        public override Guid ComponentGuid => new Guid("E8F2A9B3-D67C-45F1-BA8E-C95D30A2B714");
    }
}
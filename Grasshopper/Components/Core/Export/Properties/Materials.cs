using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;

namespace Grasshopper.Export
{
    public class MaterialCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MaterialCollector class.
        /// </summary>
        public MaterialCollectorComponent()
          : base("Materials", "Materials",
              "Creates material definitions that can be used in the structural model",
              "IMEG", "Properties")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each material", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Types for each material (e.g., 'Concrete', 'Steel', 'Wood')", GH_ParamAccess.list);
            pManager.AddTextParameter("Reinforcing", "R", "Reinforcing type for each material (optional)", GH_ParamAccess.list);
            pManager.AddTextParameter("Directional Symmetry", "DS", "Directional symmetry type (optional)", GH_ParamAccess.list);

            // Make parameters optional
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Materials", "M", "Material definitions for the structural model", GH_ParamAccess.list);
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
            List<string> reinforcingTypes = new List<string>();
            List<string> symmetryTypes = new List<string>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, types)) return;
            DA.GetDataList(2, reinforcingTypes); // Optional
            DA.GetDataList(3, symmetryTypes); // Optional

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No material names provided");
                return;
            }

            if (names.Count != types.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of names ({names.Count}) must match number of types ({types.Count})");
                return;
            }

            // Ensure optional lists have the right size or are empty
            if (reinforcingTypes.Count > 0 && reinforcingTypes.Count != names.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"If provided, number of reinforcing types ({reinforcingTypes.Count}) must match number of names ({names.Count})");
                return;
            }

            if (symmetryTypes.Count > 0 && symmetryTypes.Count != names.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"If provided, number of symmetry types ({symmetryTypes.Count}) must match number of names ({names.Count})");
                return;
            }

            try
            {
                // Create materials
                List<Material> materials = new List<Material>();

                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    string type = types[i];

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Empty material name or type skipped");
                        continue;
                    }

                    // Create a new material
                    Material material = new Material
                    {
                        Name = name,
                        Type = type
                    };

                    // Set optional properties if provided
                    if (reinforcingTypes.Count > i)
                    {
                        material.Reinforcing = reinforcingTypes[i];
                    }

                    if (symmetryTypes.Count > i)
                    {
                        material.DirectionalSymmetryType = symmetryTypes[i];
                    }

                    // Initialize default design data
                    // You could add more inputs to allow customization of these values
                    if (type.Equals("Concrete", StringComparison.OrdinalIgnoreCase))
                    {
                        material.DesignData["fc"] = 4000.0; // Default compressive strength in psi
                        material.DesignData["densityPCF"] = 150.0; // Default density in pcf
                    }
                    else if (type.Equals("Steel", StringComparison.OrdinalIgnoreCase))
                    {
                        material.DesignData["fy"] = 50000.0; // Default yield strength in psi
                        material.DesignData["fu"] = 65000.0; // Default ultimate strength in psi
                        material.DesignData["E"] = 29000000.0; // Default modulus of elasticity in psi
                    }
                    else if (type.Equals("Wood", StringComparison.OrdinalIgnoreCase))
                    {
                        material.DesignData["fb"] = 1000.0; // Default bending strength in psi
                        material.DesignData["E"] = 1600000.0; // Default modulus of elasticity in psi
                    }

                    materials.Add(material);
                }

                // Set output
                DA.SetDataList(0, materials);
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
        public override Guid ComponentGuid => new Guid("E8F2A9B3-D67C-45F1-BA8E-C95D30A2B714");
    }
}
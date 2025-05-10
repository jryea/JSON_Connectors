using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Grasshopper.Utilities;
using GH_Types = Grasshopper.Kernel.Types;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class WallPropertiesCollectorComponent : ComponentBase
    {
        // Initializes a new instance of the WallPropertiesCollector class.
        public WallPropertiesCollectorComponent()
          : base("Wall Properties", "WallProps",
              "Creates wall property definitions for the structural model",
              "IMEG", "Properties")
        {
        }

        // Registers all the input parameters for this component.
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Names for each wall property", GH_ParamAccess.list);
            pManager.AddGenericParameter("Material", "M", "Materials for each wall property", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "TH", "Thickness for each wall property (in inches)", GH_ParamAccess.list);
        }

        // Registers all the output parameters for this component.
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Wall Properties", "WP", "Wall property definitions for the structural model", GH_ParamAccess.list);
        }

        // This is the method that actually does the work.
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<string> names = new List<string>();
            List<object> materialObjs = new List<object>();
            List<double> thicknesses = new List<double>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, materialObjs)) return;
            if (!DA.GetDataList(2, thicknesses)) return;

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No wall property names provided");
                return;
            }

            if (names.Count != materialObjs.Count || names.Count != thicknesses.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of names ({names.Count}) must match number of materials ({materialObjs.Count}) " +
                    $"and thicknesses ({thicknesses.Count})");
                return;
            }

            try
            {
                // Create wall properties
                List<GH_WallProperties> wallPropertiesList = new List<GH_WallProperties>();

                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    Material material = ExtractMaterial(materialObjs[i]);
                    double thickness = thicknesses[i];

                    if (string.IsNullOrWhiteSpace(name) || material == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Skipping property at index {i}: Empty name or invalid material");
                        continue;
                    }

                    // Validate thickness
                    if (thickness <= 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Invalid thickness ({thickness}) for wall property '{name}'. Must be greater than zero.");
                        continue;
                    }

                    // Create a new wall property
                    WallProperties wallProperties = new WallProperties(name, material.Id, thickness);

                    // Add material-specific properties
                    if (material.Type == MaterialType.Concrete)
                    {
                        // Add concrete wall properties
                        wallProperties.Properties["fc"] = material.ConcreteProps?.Fc ?? 4000.0; // Default concrete strength in psi
                        wallProperties.Properties["reinforcementRatio"] = 0.0025; // Default minimum reinforcement ratio
                    }
                    else if (material.Type == MaterialType.Steel)
                    {
                        // Add steel wall properties
                        wallProperties.Properties["fy"] = material.SteelProps?.Fy ?? 50000.0; // Default steel yield strength in psi
                        wallProperties.Properties["studSpacing"] = 16.0; // Default stud spacing in inches
                    }

                    wallPropertiesList.Add(new GH_WallProperties(wallProperties));
                }

                // Set output
                DA.SetDataList(0, wallPropertiesList);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private Material ExtractMaterial(object obj)
        {
            // Direct type check
            if (obj is Material directMaterial)
                return directMaterial;

            // Using GooWrapper
            if (obj is GH_Material ghMaterial)
                return ghMaterial.Value;

            // Try handling string IDs (for compatibility)
            if (obj is string materialName && !string.IsNullOrWhiteSpace(materialName))
            {
                // Create a basic material
                Material material = new Material(materialName, MaterialType.Concrete);
                return material;
            }

            // Handle IGH_Goo objects that can be cast to Material
            if (obj is GH_Types.IGH_Goo goo && goo.CastTo<Material>(out var castMaterial))
            {
                return castMaterial;
            }

            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                $"Could not extract Material from input: {obj?.GetType().Name ?? "null"}");
            return null;
        }

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid => new Guid("2C3D4E5F-6A7B-8C9D-0E1F-2A3B4C5D6E7F");
    }
}
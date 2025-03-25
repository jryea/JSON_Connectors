using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Grasshopper.Utilities;
using GH_Types = Grasshopper.Kernel.Types;

namespace Grasshopper.Export
{
    public class WallPropertiesCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WallPropertiesCollector class.
        /// </summary>
        public WallPropertiesCollectorComponent()
          : base("Wall Properties", "WallProps",
              "Creates wall property definitions for the structural model",
              "IMEG", "Properties")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Names for each wall property", GH_ParamAccess.list);
            pManager.AddGenericParameter("Material", "M", "Materials for each wall property", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "TH", "Thickness for each wall property (in inches)", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Wall Properties", "WP", "Wall property definitions for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
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
                    WallProperties wallProperties = new WallProperties
                    {
                        Name = name,
                        MaterialId = material.Id,
                        Thickness = thickness
                    };

                    // Add material-specific properties
                    if (material.Type.Equals("Concrete", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add concrete wall properties
                        wallProperties.Properties["fc"] = 4000.0; // Default concrete strength in psi
                        wallProperties.Properties["reinforcementRatio"] = 0.0025; // Default minimum reinforcement ratio
                    }
                    else if (material.Type.Equals("Masonry", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add masonry wall properties
                        wallProperties.Properties["fm"] = 1500.0; // Default masonry strength in psi
                        wallProperties.Properties["isGrouted"] = true; // Default to grouted masonry
                    }
                    else if (material.Type.Equals("Wood", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add wood wall properties
                        wallProperties.Properties["studSpacing"] = 16.0; // Default stud spacing in inches
                        wallProperties.Properties["sheathing"] = "Plywood"; // Default sheathing material
                    }
                    else if (material.Type.Equals("Steel", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add steel wall properties
                        wallProperties.Properties["studGage"] = 18; // Default stud gage
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
                // Create a basic material with the provided name
                return new Material
                {
                    Name = materialName,
                    Type = DetermineMaterialTypeFromName(materialName)
                };
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

        private string DetermineMaterialTypeFromName(string materialName)
        {
            // Try to determine material type from name for backward compatibility
            materialName = materialName.ToLower();

            if (materialName.Contains("concrete") || materialName.Contains("conc"))
                return "Concrete";
            else if (materialName.Contains("steel") || materialName.Contains("metal"))
                return "Steel";
            else if (materialName.Contains("wood") || materialName.Contains("timber"))
                return "Wood";
            else if (materialName.Contains("masonry") || materialName.Contains("brick") || materialName.Contains("cmu"))
                return "Masonry";
            else
                return "Unknown";
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
        public override Guid ComponentGuid => new Guid("2C3D4E5F-6A7B-8C9D-0E1F-2A3B4C5D6E7F");
    }
}
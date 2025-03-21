using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;

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
            pManager.AddTextParameter("Names", "N", "Names for each wall property", GH_ParamAccess.list);
            pManager.AddTextParameter("Materials", "M", "Materials for each wall property (e.g., 'Concrete', 'Masonry', 'Wood')", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thicknesses", "TH", "Thicknesses for each wall property (in inches)", GH_ParamAccess.list);
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
            List<string> materials = new List<string>();
            List<double> thicknesses = new List<double>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, materials)) return;
            if (!DA.GetDataList(2, thicknesses)) return;

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No wall property names provided");
                return;
            }

            if (names.Count != materials.Count || names.Count != thicknesses.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of names ({names.Count}) must match number of materials ({materials.Count}) " +
                    $"and thicknesses ({thicknesses.Count})");
                return;
            }

            try
            {
                // Create wall properties
                List<WallProperties> wallPropertiesList = new List<WallProperties>();

                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    string material = materials[i];
                    double thickness = thicknesses[i];

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(material))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Empty wall property name or material skipped");
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
                        Material = material,
                        Thickness = thickness
                    };

                    // Add material-specific properties
                    if (material.Equals("Concrete", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add concrete wall properties
                        wallProperties.Properties["fc"] = 4000.0; // Default concrete strength in psi
                        wallProperties.Properties["reinforcementRatio"] = 0.0025; // Default minimum reinforcement ratio
                    }
                    else if (material.Equals("Masonry", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add masonry wall properties
                        wallProperties.Properties["fm"] = 1500.0; // Default masonry strength in psi
                        wallProperties.Properties["isGrouted"] = true; // Default to grouted masonry
                    }
                    else if (material.Equals("Wood", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add wood wall properties
                        wallProperties.Properties["studSpacing"] = 16.0; // Default stud spacing in inches
                        wallProperties.Properties["sheathing"] = "Plywood"; // Default sheathing material
                    }
                    else if (material.Equals("Steel", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add steel wall properties
                        wallProperties.Properties["studGage"] = 18; // Default stud gage
                        wallProperties.Properties["studSpacing"] = 16.0; // Default stud spacing in inches
                    }

                    wallPropertiesList.Add(wallProperties);
                }

                // Set output
                DA.SetDataList(0, wallPropertiesList);
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
        public override Guid ComponentGuid => new Guid("2C3D4E5F-6A7B-8C9D-0E1F-2A3B4C5D6E7F");
    }
}
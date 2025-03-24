using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;

namespace Grasshopper.Export
{
    public class FloorPropertiesCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FloorPropertiesCollector class.
        /// </summary>
        public FloorPropertiesCollectorComponent()
          : base("Floor Properties", "FloorProps",
              "Creates floor property definitions for the structural model",
              "IMEG", "Properties")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Name for each floor property", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Type for floor (e.g., 'Slab', 'Composite', 'NonComposite')", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thicknesses", "TH", "Thickness (in inches)", GH_ParamAccess.list);
            pManager.AddGenericParameter("Materials", "M", "Material", GH_ParamAccess.list);
            pManager.AddTextParameter("Reinforcement", "R", "Reinforcement (optional)", GH_ParamAccess.list);

            // Make some parameters optional
            pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Floor Properties", "FP", "Floor property definitions for the structural model", GH_ParamAccess.list);
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
            List<double> thicknesses = new List<double>();
            List<Material> materials = new List<Material>();
            List<string> reinforcements = new List<string>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, types)) return;
            if (!DA.GetDataList(2, thicknesses)) return;
            if (!DA.GetDataList(3, materials)) return;
            DA.GetDataList(4, reinforcements); // Optional

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No floor property names provided");
                return;
            }

            if (names.Count != types.Count || names.Count != thicknesses.Count || names.Count != materials.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of names ({names.Count}) must match number of types ({types.Count}), " +
                    $"thicknesses ({thicknesses.Count}), and material IDs ({materials.Count})");
                return;
            }

            // Ensure optional reinforcements have the right size or are empty
            if (reinforcements.Count > 0 && reinforcements.Count != names.Count)
            {
                if (reinforcements.Count == 1)
                {
                    // Use the single value for all properties
                    string reinforcement = reinforcements[0];
                    reinforcements.Clear();
                    for (int i = 0; i < names.Count; i++)
                        reinforcements.Add(reinforcement);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Number of reinforcements ({reinforcements.Count}) must match number of names ({names.Count}) or be a single value");
                    return;
                }
            }

            try
            {
                // Create floor properties
                List<FloorProperties> floorPropertiesList = new List<FloorProperties>();

                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    string type = types[i];
                    double thickness = thicknesses[i];
                    Material material = materials[i];

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type) || material != null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Empty floor property name, type, or material ID skipped");
                        continue;
                    }

                    // Validate thickness
                    if (thickness <= 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Invalid thickness ({thickness}) for floor property '{name}'. Must be greater than zero.");
                        continue;
                    }

                    // Create a new floor property
                    FloorProperties floorProperties = new FloorProperties
                    {
                        Name = name,
                        Type = type,
                        Thickness = thickness,
                        Material = material
                    };

                    // Set reinforcement if provided
                    if (reinforcements.Count > i)
                    {
                        floorProperties.Reinforcement = reinforcements[i];
                    }

                    // Add type-specific properties
                    if (type.Equals("Concrete", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add concrete slab properties
                        floorProperties.SlabProperties["isRibbed"] = false;
                        floorProperties.SlabProperties["isWaffle"] = false;
                        floorProperties.SlabProperties["isTwoWay"] = true;
                    }
                    else if (type.Equals("Composite", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add composite deck properties
                        floorProperties.DeckProperties["deckType"] = "Composite";
                        floorProperties.DeckProperties["deckDepth"] = 1.5; // Default deck depth in inches
                        floorProperties.DeckProperties["deckGage"] = 22;   // Default deck gage
                        floorProperties.DeckProperties["toppingThickness"] = thickness - 1.5;
                    }
                    else if (type.Equals("Deck", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add metal deck properties
                        floorProperties.DeckProperties["deckType"] = "Metal";
                        floorProperties.DeckProperties["deckDepth"] = thickness;
                        floorProperties.DeckProperties["deckGage"] = 22;  // Default deck gage
                    }

                    floorPropertiesList.Add(floorProperties);
                }

                // Set output
                DA.SetDataList(0, floorPropertiesList);
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
        public override Guid ComponentGuid => new Guid("5A6B7C8D-9E0F-1A2B-3C4D-5E6F7A8B9C0D");
    }
}
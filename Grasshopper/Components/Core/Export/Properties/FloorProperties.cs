using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Grasshopper.Utilities;
using GH_Type = Grasshopper.Kernel.Types;

namespace Grasshopper.Components.Core.Export.Properties
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
        protected override void RegisterInputParams(GH_InputParamManager pManager)
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
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
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
            List<object> materialObjs = new List<object>();
            List<string> reinforcements = new List<string>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, types)) return;
            if (!DA.GetDataList(2, thicknesses)) return;
            if (!DA.GetDataList(3, materialObjs)) return;
            DA.GetDataList(4, reinforcements); // Optional

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No floor property names provided");
                return;
            }

            if (names.Count != types.Count || names.Count != thicknesses.Count || names.Count != materialObjs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of names ({names.Count}) must match number of types ({types.Count}), " +
                    $"thicknesses ({thicknesses.Count}), and materials ({materialObjs.Count})");
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
                List<GH_FloorProperties> floorPropertiesList = new List<GH_FloorProperties>();

                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    string type = types[i];
                    double thickness = thicknesses[i];
                    Material material = ExtractMaterial(materialObjs[i]);

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type) || material == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Skipping property at index {i}: Empty name, type, or invalid material");
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
                        MaterialId = material.Id
                    };

                    // Set reinforcement if provided
                    if (reinforcements.Count > i)
                    {
                        floorProperties.Reinforcement = reinforcements[i];
                    }

                    // Add type-specific properties
                    if (type.Equals("Slab", StringComparison.OrdinalIgnoreCase) ||
                        type.Equals("Concrete", StringComparison.OrdinalIgnoreCase))
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
                    else if (type.Equals("Deck", StringComparison.OrdinalIgnoreCase) ||
                             type.Equals("NonComposite", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add metal deck properties
                        floorProperties.DeckProperties["deckType"] = "Metal";
                        floorProperties.DeckProperties["deckDepth"] = thickness;
                        floorProperties.DeckProperties["deckGage"] = 22;  // Default deck gage
                    }

                    floorPropertiesList.Add(new GH_FloorProperties(floorProperties));
                }

                // Set output
                DA.SetDataList(0, floorPropertiesList);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private Material ExtractMaterial(object materialObj)
        {
            // Direct reference
            if (materialObj is Material material)
                return material;

            // Through wrapper
            if (materialObj is GH_Material ghMaterial)
                return ghMaterial.Value;

            // String ID based lookup (for backward compatibility)
            if (materialObj is string materialName && !string.IsNullOrWhiteSpace(materialName))
            {
                // Create a basic material with the provided name
                // In a real-world scenario, you might want to look up the material in a repository
                return new Material
                {
                    Name = materialName,
                    Type = DetermineMaterialTypeFromName(materialName)
                };
            }

            // If we have a generic GH_Goo object, try to extract the value
            if (materialObj is GH_Type.IGH_Goo goo && goo.CastTo<Material>(out var castMaterial))
            {
                return castMaterial;
            }

            // Log warning and return null if we couldn't extract a material
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                $"Could not extract Material from input: {materialObj?.GetType().Name ?? "null"}");
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
        public override Guid ComponentGuid => new Guid("5A6B7C8D-9E0F-1A2B-3C4D-5E6F7A8B9C0D");
    }
}
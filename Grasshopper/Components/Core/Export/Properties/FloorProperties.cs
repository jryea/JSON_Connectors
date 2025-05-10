using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Grasshopper.Utilities;
using GH_Type = Grasshopper.Kernel.Types;
using Core.Models.ModelLayout;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class FloorPropertiesCollectorComponent : ComponentBase
    {
        // Initializes a new instance of the FloorPropertiesCollector class.
        public FloorPropertiesCollectorComponent()
          : base("Floor Properties", "FloorProps",
              "Creates floor property definitions for the structural model",
              "IMEG", "Properties")
        {
        }

        // Registers all the input parameters for this component.
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Name for each floor property", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Type for floor (e.g., 'Slab', 'FilledDeck', 'UnfilledDeck')", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thicknesses", "TH", "Thickness (in inches)", GH_ParamAccess.list);
            pManager.AddGenericParameter("Materials", "M", "Material", GH_ParamAccess.list);

            // Make some parameters optional
            pManager[1].Optional = true;  // Types
        }

        // Registers all the output parameters for this component.
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Floor Properties", "FP", "Floor property definitions for the structural model", GH_ParamAccess.list);
        }

        // This is the method that actually does the work.
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<string> names = new List<string>();
            List<string> typeStrings = new List<string>();
            List<double> thicknesses = new List<double>();
            List<object> materialObjs = new List<object>();

            if (!DA.GetDataList(0, names)) return;
            DA.GetDataList(1, typeStrings);
            if (!DA.GetDataList(2, thicknesses)) return;
            if (!DA.GetDataList(3, materialObjs)) return;

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No floor property names provided");
                return;
            }

            // Set default types if not provided
            if (typeStrings.Count == 0)
            {
                typeStrings = new List<string>(new string[names.Count]);
                for (int i = 0; i < names.Count; i++)
                    typeStrings[i] = "Slab";
            }

            // Extend lists to match names length by duplicating the last item
            if (typeStrings.Count > 0 && typeStrings.Count < names.Count)
            {
                string lastType = typeStrings[typeStrings.Count - 1];
                while (typeStrings.Count < names.Count)
                    typeStrings.Add(lastType);
            }

            if (thicknesses.Count > 0 && thicknesses.Count < names.Count)
            {
                double lastThickness = thicknesses[thicknesses.Count - 1];
                while (thicknesses.Count < names.Count)
                    thicknesses.Add(lastThickness);
            }

            if (materialObjs.Count > 0 && materialObjs.Count < names.Count)
            {
                object lastMaterial = materialObjs[materialObjs.Count - 1];
                while (materialObjs.Count < names.Count)
                    materialObjs.Add(lastMaterial);
            }

            // Verify list lengths again after extension
            if (names.Count != typeStrings.Count || names.Count != thicknesses.Count || names.Count != materialObjs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of names ({names.Count}) must match number of types ({typeStrings.Count}), " +
                    $"thicknesses ({thicknesses.Count}), and materials ({materialObjs.Count})");
                return;
            }

            try
            {
                // Create floor properties
                List<GH_FloorProperties> floorPropertiesList = new List<GH_FloorProperties>();

                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    string typeString = typeStrings[i];
                    double thickness = thicknesses[i];
                    Material material = ExtractMaterial(materialObjs[i]);

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(typeString) || material == null)
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

                    // Parse floor type enum
                    StructuralFloorType floorType;
                    if (!Enum.TryParse(typeString, true, out floorType))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Unknown floor type: {typeString}, defaulting to Slab");
                        floorType = StructuralFloorType.Slab;
                    }

                    // Create a new floor property
                    FloorProperties floorProperties = new FloorProperties(name, floorType, thickness, material.Id);
                    floorProperties.Thickness = thickness;

                    // Configure properties based on floor type
                    if (floorType == StructuralFloorType.FilledDeck || floorType == StructuralFloorType.UnfilledDeck)
                    {
                        // Set common deck properties
                        floorProperties.DeckProperties.RibDepth = 3.0;  // Default in inches
                        floorProperties.DeckProperties.RibWidthTop = 6.0;
                        floorProperties.DeckProperties.RibWidthBottom = 5.0;
                        floorProperties.DeckProperties.RibSpacing = 12.0;
                        floorProperties.DeckProperties.DeckShearThickness = 0.035;

                        if (floorType == StructuralFloorType.FilledDeck)
                        {
                            // For composite decks, set shear stud properties
                            floorProperties.ShearStudProperties.ShearStudDiameter = 0.75;
                            floorProperties.ShearStudProperties.ShearStudHeight = 6.0;
                            floorProperties.ShearStudProperties.ShearStudTensileStrength = 65000;
                        }
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
                return new Material(materialName, MaterialType.Concrete);
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

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid => new Guid("5A6B7C8D-9E0F-1A2B-3C4D-5E6F7A8B9C0D");
    }
}
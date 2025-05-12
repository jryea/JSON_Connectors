using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Grasshopper.Utilities;
using GH_Type = Grasshopper.Kernel.Types;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class FloorPropertiesCollectorComponent : ComponentBase
    {
        public FloorPropertiesCollectorComponent()
          : base("Floor Properties", "FloorProps",
              "Creates floor property definitions for the structural model",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Name for each floor property", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Floor type (Slab, FilledDeck, UnfilledDeck, SolidSlabDeck)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "TH", "Thickness (in inches)", GH_ParamAccess.list);
            pManager.AddGenericParameter("Material", "M", "Material", GH_ParamAccess.list);
            pManager.AddTextParameter("Modeling Type", "MT", "Modeling type (ShellThin, ShellThick, Membrane, Layered)", GH_ParamAccess.list, "Membrane");
            pManager.AddTextParameter("Slab Type", "ST", "Slab type (Slab, Drop, Stiff, Ribbed, Waffle, Mat, Footing)", GH_ParamAccess.list, "Slab");
            pManager.AddGenericParameter("Deck Properties", "DP", "Deck properties for filled/unfilled deck types", GH_ParamAccess.item);
            pManager.AddGenericParameter("Shear Stud Properties", "SP", "Shear stud properties for composite decks", GH_ParamAccess.item);

            // Make parameters optional
            pManager[1].Optional = true;  // Types
            pManager[4].Optional = true;  // Modeling Type
            pManager[5].Optional = true;  // Slab Type
            pManager[6].Optional = true;  // Deck Properties
            pManager[7].Optional = true;  // Shear Stud Properties
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Floor Properties", "FP", "Floor property definitions for the structural model", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<string> names = new List<string>();
            List<string> typeStrings = new List<string>();
            List<double> thicknesses = new List<double>();
            List<object> materialObjs = new List<object>();
            List<string> modelingTypeStrings = new List<string>();
            List<string> slabTypeStrings = new List<string>();
            object deckPropObj = null;
            object shearStudPropObj = null;

            if (!DA.GetDataList(0, names)) return;
            DA.GetDataList(1, typeStrings);
            if (!DA.GetDataList(2, thicknesses)) return;
            if (!DA.GetDataList(3, materialObjs)) return;
            DA.GetDataList(4, modelingTypeStrings);
            DA.GetDataList(5, slabTypeStrings);
            DA.GetData(6, ref deckPropObj);
            DA.GetData(7, ref shearStudPropObj);

            // Set default types if not provided
            if (typeStrings.Count == 0)
            {
                typeStrings = new List<string>(new string[names.Count]);
                for (int i = 0; i < names.Count; i++)
                    typeStrings[i] = "Slab";
            }

            // Extend all lists to match names length
            EnsureListLength(ref typeStrings, names.Count, typeStrings.Count > 0 ? typeStrings[typeStrings.Count - 1] : "Slab");
            EnsureListLength(ref thicknesses, names.Count, thicknesses.Count > 0 ? thicknesses[thicknesses.Count - 1] : 0.0);

            if (materialObjs.Count > 0 && materialObjs.Count < names.Count)
            {
                object lastMaterial = materialObjs[materialObjs.Count - 1];
                while (materialObjs.Count < names.Count)
                    materialObjs.Add(lastMaterial);
            }

            EnsureListLength(ref modelingTypeStrings, names.Count, modelingTypeStrings.Count > 0 ? modelingTypeStrings[modelingTypeStrings.Count - 1] : "Membrane");
            EnsureListLength(ref slabTypeStrings, names.Count, slabTypeStrings.Count > 0 ? slabTypeStrings[slabTypeStrings.Count - 1] : "Slab");

            // Extract deck and shear stud properties
            DeckProperties deckProps = ExtractDeckProperties(deckPropObj);
            ShearStudProperties shearStudProps = ExtractShearStudProperties(shearStudPropObj);

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

                    if (string.IsNullOrWhiteSpace(name) || material == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Skipping property at index {i}: Empty name or invalid material");
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

                    // Parse modeling type enum
                    ModelingType modelingType = ModelingType.Membrane;
                    if (modelingTypeStrings.Count > i && !string.IsNullOrEmpty(modelingTypeStrings[i]))
                    {
                        if (!Enum.TryParse(modelingTypeStrings[i], true, out modelingType))
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                                $"Unknown modeling type: {modelingTypeStrings[i]}, defaulting to Membrane");
                        }
                    }

                    // Parse slab type enum
                    SlabType slabType = SlabType.Slab;
                    if (slabTypeStrings.Count > i && !string.IsNullOrEmpty(slabTypeStrings[i]))
                    {
                        if (!Enum.TryParse(slabTypeStrings[i], true, out slabType))
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                                $"Unknown slab type: {slabTypeStrings[i]}, defaulting to Slab");
                        }
                    }

                    // Create a new floor property
                    FloorProperties floorProperties = new FloorProperties(name, floorType, thickness, material.Id);

                    // Set additional properties
                    floorProperties.ModelingType = modelingType;
                    floorProperties.SlabType = slabType;

                    // Add deck and shear stud properties if provided
                    if (deckProps != null && (floorType == StructuralFloorType.FilledDeck || floorType == StructuralFloorType.UnfilledDeck))
                    {
                        floorProperties.DeckProperties = deckProps;
                    }

                    if (shearStudProps != null && floorType == StructuralFloorType.FilledDeck)
                    {
                        floorProperties.ShearStudProperties = shearStudProps;
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

        private Material ExtractMaterial(object obj)
        {
            // Direct type check
            if (obj is Material directMaterial)
                return directMaterial;

            // Using GooWrapper
            if (obj is GH_Material ghMaterial)
                return ghMaterial.Value;

            // String ID based lookup (for backward compatibility)
            if (obj is string materialName && !string.IsNullOrWhiteSpace(materialName))
            {
                // Create a basic material with the provided name
                return new Material(materialName, MaterialType.Concrete);
            }

            // Handle IGH_Goo objects that can be cast to Material
            if (obj is GH_Type.IGH_Goo goo && goo.CastTo<Material>(out var castMaterial))
            {
                return castMaterial;
            }

            // Log warning and return null if we couldn't extract a material
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                $"Could not extract Material from input: {obj?.GetType().Name ?? "null"}");
            return null;
        }

        private DeckProperties ExtractDeckProperties(object obj)
        {
            if (obj == null) return null;

            // Direct type check
            if (obj is DeckProperties props)
                return props;

            // Using GooWrapper
            if (obj is GH_DeckProperties ghProps)
                return ghProps.Value;

            // Handle IGH_Goo objects
            if (obj is GH_Type.IGH_Goo goo && goo.CastTo<DeckProperties>(out var castProps))
            {
                return castProps;
            }

            return null;
        }

        private ShearStudProperties ExtractShearStudProperties(object obj)
        {
            if (obj == null) return null;

            // Direct type check
            if (obj is ShearStudProperties props)
                return props;

            // Using GooWrapper
            if (obj is GH_ShearStudProperties ghProps)
                return ghProps.Value;

            // Handle IGH_Goo objects
            if (obj is GH_Type.IGH_Goo goo && goo.CastTo<ShearStudProperties>(out var castProps))
            {
                return castProps;
            }

            return null;
        }

        private void EnsureListLength<T>(ref List<T> list, int targetLength, T defaultValue)
        {
            if (list.Count < targetLength)
            {
                while (list.Count < targetLength)
                    list.Add(defaultValue);
            }
        }

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid => new Guid("5A6B7C8D-9E0F-1A2B-3C4D-5E6F7A8B9C0D");
    }
}
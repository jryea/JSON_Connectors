using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models;
using Core.Models.Properties;
using Grasshopper.Utilities;
using GH_Types = Grasshopper.Kernel.Types;
using static Core.Models.Properties.Modifiers;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class FramePropertiesCollectorComponent : ComponentBase
    {
        // Initializes a new instance of the FramePropertiesCollector class.
        public FramePropertiesCollectorComponent()
          : base("Frame Properties", "FrameProp",
              "Creates frame property definitions for beams, columns, and braces",
              "IMEG", "Properties")
        {
        }

        // Registers all the input parameters for this component.
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name for the frame property", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material", "M", "Material for the frame property", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Material type ('Steel' or 'Concrete')", GH_ParamAccess.item, "Steel");
            pManager.AddGenericParameter("Concrete Frame Props", "CFP", "Concrete frame properties (for concrete sections)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Steel Frame Props", "SFP", "Steel frame properties (for steel sections)", GH_ParamAccess.item);
            pManager.AddGenericParameter("ETABS Modifiers", "EM", "ETABS-specific frame modifiers", GH_ParamAccess.item);

            // Make some parameters optional
            pManager[2].Optional = true;  // Type
            pManager[3].Optional = true;  // Concrete Frame Props
            pManager[4].Optional = true;  // Steel Frame Props
            pManager[5].Optional = true;  // ETABS Modifiers
        }

        // Registers all the output parameters for this component.
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Frame Property", "FP", "Frame property definition for the structural model", GH_ParamAccess.item);
        }

        // This is the method that actually does the work.
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            string name = string.Empty;
            Material material = null;
            string typeName = "Steel";
            object concretePropsObj = null;
            object steelPropsObj = null;
            object etabsModObj = null;

            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetData(1, ref material)) return;
            DA.GetData(2, ref typeName);
            DA.GetData(3, ref concretePropsObj);
            DA.GetData(4, ref steelPropsObj);
            DA.GetData(5, ref etabsModObj);

            // Basic validation
            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Frame property name cannot be empty");
                return;
            }

            if (material == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid material provided");
                return;
            }

            try
            {
                // Parse frame material type
                FrameMaterialType materialType;
                if (!Enum.TryParse(typeName, true, out materialType))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unknown frame material type: {typeName}, defaulting to Steel");
                    materialType = FrameMaterialType.Steel;
                }

                // Create a new frame property
                FrameProperties frameProperty = new FrameProperties(name, material.Id, materialType);

                // Extract ETABS modifiers if provided
                FrameModifiers rameModifiers = ExtractObject<FrameModifiers>(etabsModObj, "ETABSFrameModifiers");
                if (rameModifiers != null)
                {
                    frameProperty.FrameModifiers = rameModifiers;
                }

                // Set section properties based on material type and provided inputs
                if (materialType == FrameMaterialType.Steel)
                {
                    SteelFrameProperties steelProps = ExtractObject<SteelFrameProperties>(steelPropsObj, "SteelFrameProperties");

                    if (steelProps != null)
                    {
                        frameProperty.SteelProps = steelProps;
                    }
                    else
                    {
                        // Create default steel properties if none provided
                        frameProperty.SteelProps = new SteelFrameProperties
                        {
                            SectionType = SteelSectionType.W,
                            SectionName = "W12X26"
                        };
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                            "No steel frame properties provided, using defaults (W12X26)");
                    }
                }
                else // Concrete
                {
                    ConcreteFrameProperties concreteProps = ExtractObject<ConcreteFrameProperties>(concretePropsObj, "ConcreteFrameProperties");

                    if (concreteProps != null)
                    {
                        frameProperty.ConcreteProps = concreteProps;
                    }
                    else
                    {
                        // Create default concrete properties with proper Width and Depth
                        frameProperty.ConcreteProps = new ConcreteFrameProperties
                        {
                            SectionType = ConcreteSectionType.Rectangular,
                            SectionName = "12x12",
                            Width = 12.0,
                            Depth = 12.0
                        };
                        frameProperty.ConcreteProps.Dimensions["width"] = "12";
                        frameProperty.ConcreteProps.Dimensions["depth"] = "12";

                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                            "No concrete frame properties provided, using defaults (12x12 rectangular)");
                    }
                }

                // Output the frame property
                DA.SetData(0, new Utilities.GH_FrameProperties(frameProperty));
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private T ExtractObject<T>(object obj, string typeName) where T : class
        {
            // Add detailed logging
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                $"ExtractObject<{typeName}>: Input type = {obj?.GetType().Name ?? "null"}");

            if (obj == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"ExtractObject<{typeName}>: Input is null");
                return null;
            }

            // Direct type check
            if (obj is T directType)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"ExtractObject<{typeName}>: Direct type match found");
                return directType;
            }

            // Using GooWrapper
            if (obj is GH_ModelGoo<T> ghType)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"ExtractObject<{typeName}>: GH_ModelGoo wrapper found");
                return ghType.Value;
            }

            // Specific check for SteelFrameProperties
            if (typeof(T) == typeof(SteelFrameProperties) && obj is GH_SteelFrameProperties ghSteelProps)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"ExtractObject<{typeName}>: GH_SteelFrameProperties found directly");
                return ghSteelProps.Value as T;
            }

            // Specific check for ConcreteFrameProperties  
            if (typeof(T) == typeof(ConcreteFrameProperties) && obj is GH_ConcreteFrameProperties ghConcreteProps)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"ExtractObject<{typeName}>: GH_ConcreteFrameProperties found directly");
                return ghConcreteProps.Value as T;
            }

            // Handle IGH_Goo objects that can be cast
            if (obj is GH_Types.IGH_Goo goo)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"ExtractObject<{typeName}>: IGH_Goo found, attempting cast");
                T result = null;
                bool success = goo.CastTo<T>(out result);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"ExtractObject<{typeName}>: Cast success = {success}");
                if (success)
                    return result;
            }

            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                $"Could not extract {typeName} from input of type {obj?.GetType().Name ?? "null"}");
            return null;
        }

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid => new Guid("D1E2F3A4-B5C6-D7E8-F9A0-B1C2D3E4F5A6");
    }
}
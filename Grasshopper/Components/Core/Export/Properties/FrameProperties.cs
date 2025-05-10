using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;

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
            pManager.AddTextParameter("Section Type", "ST", "Section type (W, HSS, PIPE for Steel; Rectangular, Circular for Concrete)", GH_ParamAccess.item);
            pManager.AddTextParameter("Section Name", "SN", "Section name (optional)", GH_ParamAccess.item);

            // Make some parameters optional
            pManager[2].Optional = true;
            pManager[4].Optional = true;
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
            string sectionTypeName = string.Empty;
            string sectionName = string.Empty;

            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetData(1, ref material)) return;
            DA.GetData(2, ref typeName);
            if (!DA.GetData(3, ref sectionTypeName)) return;
            DA.GetData(4, ref sectionName);

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
                FrameProperties.FrameMaterialType materialType;
                if (!Enum.TryParse(typeName, true, out materialType))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unknown frame material type: {typeName}, defaulting to Steel");
                    materialType = FrameProperties.FrameMaterialType.Steel;
                }

                // Create a new frame property
                FrameProperties frameProperty = new FrameProperties(name, material.Id, materialType);

                // Set section properties based on material type
                if (materialType == FrameProperties.FrameMaterialType.Steel)
                {
                    frameProperty.SteelProps = new SteelFrameProperties();

                    // Parse steel section type
                    SteelFrameProperties.SteelSectionType sectionType;
                    if (!Enum.TryParse(sectionTypeName, true, out sectionType))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Unknown steel section type: {sectionTypeName}, defaulting to W");
                        sectionType = SteelFrameProperties.SteelSectionType.W;
                    }

                    frameProperty.SteelProps.SectionType = sectionType;
                    frameProperty.SteelProps.SectionName = string.IsNullOrEmpty(sectionName) ?
                        $"{sectionType}12X26" : sectionName; // Default section name if not provided
                }
                else // Concrete
                {
                    frameProperty.ConcreteProps = new ConcreteFrameProperties();

                    // Parse concrete section type
                    ConcreteFrameProperties.ConcreteSectionType sectionType;
                    if (!Enum.TryParse(sectionTypeName, true, out sectionType))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Unknown concrete section type: {sectionTypeName}, defaulting to Rectangular");
                        sectionType = ConcreteFrameProperties.ConcreteSectionType.Rectangular;
                    }

                    frameProperty.ConcreteProps.SectionType = sectionType;
                    frameProperty.ConcreteProps.SectionName = string.IsNullOrEmpty(sectionName) ?
                        "12x12" : sectionName; // Default section name if not provided

                    // Add default dimensions for concrete sections
                    if (sectionType == ConcreteFrameProperties.ConcreteSectionType.Rectangular)
                    {
                        frameProperty.ConcreteProps.Dimensions["width"] = "12";
                        frameProperty.ConcreteProps.Dimensions["depth"] = "12";
                    }
                    else if (sectionType == ConcreteFrameProperties.ConcreteSectionType.Circular)
                    {
                        frameProperty.ConcreteProps.Dimensions["diameter"] = "18";
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

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid => new Guid("D1E2F3A4-B5C6-D7E8-F9A0-B1C2D3E4F5A6");
    }
}
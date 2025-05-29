using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Core.Models;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class ConcreteFramePropertiesComponent : ComponentBase
    {
        public ConcreteFramePropertiesComponent()
          : base("Concrete Frame Properties", "ConcFrameProps",
              "Creates concrete-specific frame properties for beams, columns, and braces",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Section Type", "ST", "Concrete section type (Rectangular, Circular, TShaped, LShaped, Custom)", GH_ParamAccess.item, "Rectangular");
            pManager.AddTextParameter("Section Name", "SN", "Section name or designation", GH_ParamAccess.item, "12x12");
            pManager.AddTextParameter("Width", "W", "Width dimension (for rectangular sections)", GH_ParamAccess.item, "12");
            pManager.AddTextParameter("Depth", "D", "Depth dimension (for rectangular sections)", GH_ParamAccess.item, "12");
            pManager.AddTextParameter("Diameter", "DIA", "Diameter (for circular sections)", GH_ParamAccess.item, "18");

            // Make parameters optional
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Concrete Frame Properties", "CFP", "Concrete-specific frame properties", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string sectionTypeName = "Rectangular";
            string sectionName = "12x12";
            string width = "12";
            string depth = "12";
            string diameter = "18";

            DA.GetData(0, ref sectionTypeName);
            DA.GetData(1, ref sectionName);
            DA.GetData(2, ref width);
            DA.GetData(3, ref depth);
            DA.GetData(4, ref diameter);

            try
            {
                // Parse concrete section type
                ConcreteSectionType sectionType;
                if (!Enum.TryParse(sectionTypeName, true, out sectionType))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unknown concrete section type: {sectionTypeName}, defaulting to Rectangular");
                    sectionType = ConcreteSectionType.Rectangular;
                }

                // Create concrete frame properties
                ConcreteFrameProperties concreteProps = new ConcreteFrameProperties
                {
                    SectionType = sectionType,
                    SectionName = string.IsNullOrEmpty(sectionName) ? "12x12" : sectionName
                };

                // Set dimensions based on section type
                if (sectionType == ConcreteSectionType.Rectangular)
                {
                    concreteProps.Dimensions["width"] = string.IsNullOrEmpty(width) ? "12" : width;
                    concreteProps.Dimensions["depth"] = string.IsNullOrEmpty(depth) ? "12" : depth;
                }
                else if (sectionType == ConcreteSectionType.Circular)
                {
                    concreteProps.Dimensions["diameter"] = string.IsNullOrEmpty(diameter) ? "18" : diameter;
                }
                else if (sectionType == ConcreteSectionType.TShaped || sectionType == ConcreteSectionType.LShaped)
                {
                    // For T and L shaped sections, use width and depth as starting dimensions
                    concreteProps.Dimensions["width"] = string.IsNullOrEmpty(width) ? "12" : width;
                    concreteProps.Dimensions["depth"] = string.IsNullOrEmpty(depth) ? "12" : depth;
                    // Additional dimensions would be added here for complex shapes
                }

                // Output the concrete frame properties
                DA.SetData(0, new GH_ConcreteFrameProperties(concreteProps));
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        public override Guid ComponentGuid => new Guid("A1B2C3D4-E5F6-7890-1234-567890ABCDEF");
    }
}
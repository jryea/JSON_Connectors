using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Core.Models.Properties;
using Core.Models;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class SteelFramePropertiesComponent : ComponentBase
    {
        public SteelFramePropertiesComponent()
          : base("Steel Frame Properties", "SteelFrameProps",
              "Creates steel-specific frame properties for beams, columns, and braces",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Section Type", "ST", "Steel section type (W, HSS, PIPE, C, L, WT, ST, MC, HP)", GH_ParamAccess.item, "W");
            pManager.AddTextParameter("Section Name", "SN", "Section name or designation (e.g., W12X26, HSS6X6X1/4)", GH_ParamAccess.item, "W12X26");

            // Make parameters optional
            pManager[0].Optional = true;
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Steel Frame Properties", "SFP", "Steel-specific frame properties", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string sectionTypeName = "W";
            string sectionName = "W12X26";

            DA.GetData(0, ref sectionTypeName);
            DA.GetData(1, ref sectionName);

            try
            {
                // Parse steel section type
                SteelSectionType sectionType;
                if (!Enum.TryParse(sectionTypeName, true, out sectionType))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Unknown steel section type: {sectionTypeName}, defaulting to W");
                    sectionType = SteelSectionType.W;
                }

                // Create steel frame properties
                SteelFrameProperties steelProps = new SteelFrameProperties
                {
                    SectionType = sectionType,
                    SectionName = string.IsNullOrEmpty(sectionName) ? $"{sectionType}12X26" : sectionName
                };

                // Output the steel frame properties
                DA.SetData(0, new GH_SteelFrameProperties(steelProps));
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        public override Guid ComponentGuid => new Guid("B2C3D4E5-F6A7-8901-2345-67890ABCDEF1");
    }
}
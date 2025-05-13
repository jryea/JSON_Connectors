using Grasshopper.Kernel;
using RG = Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Geometry;
using Grasshopper.Utilities;
using static Core.Models.SoftwareSpecific.ETABSModifiers;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class BraceCollectorComponent : ComponentBase
    {
        public BraceCollectorComponent()
          : base("Braces", "Braces",
              "Creates brace objects that can be used in the structural model",
              "IMEG", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Lines representing braces", GH_ParamAccess.list);
            pManager.AddGenericParameter("Base Level", "BL", "Base level of the brace", GH_ParamAccess.list);
            pManager.AddGenericParameter("Top Level", "TL", "Top level of the brace", GH_ParamAccess.list);
            pManager.AddGenericParameter("Frame Properties", "P", "Frame properties for this brace", GH_ParamAccess.list);
            pManager.AddGenericParameter("ETABS Modifiers", "EM", "ETABS-specific frame modifiers", GH_ParamAccess.list);

            // Make ETABS modifiers optional
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Braces", "B", "Brace objects for the structural model", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<RG.Line> lines = new List<RG.Line>();
            List<object> baseLevelObjs = new List<object>();
            List<object> topLevelObjs = new List<object>();
            List<object> framePropObjs = new List<object>();
            List<object> etabsModObjs = new List<object>();

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, baseLevelObjs)) return;
            if (!DA.GetDataList(2, topLevelObjs)) return;
            if (!DA.GetDataList(3, framePropObjs)) return;
            DA.GetDataList(4, etabsModObjs);

            if (lines.Count != baseLevelObjs.Count || lines.Count != topLevelObjs.Count || lines.Count != framePropObjs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of lines must match number of levels and properties");
                return;
            }

            // Extend ETABS modifiers list if needed
            if (etabsModObjs.Count > 0 && etabsModObjs.Count < lines.Count)
            {
                object lastMod = etabsModObjs[etabsModObjs.Count - 1];
                while (etabsModObjs.Count < lines.Count)
                {
                    etabsModObjs.Add(lastMod);
                }
            }

            List<GH_Brace> braces = new List<GH_Brace>();
            for (int i = 0; i < lines.Count; i++)
            {
                RG.Line line = lines[i];
                Level baseLevel = ExtractObject<Level>(baseLevelObjs[i], "BaseLevel");
                Level topLevel = ExtractObject<Level>(topLevelObjs[i], "TopLevel");
                FrameProperties frameProps = ExtractObject<FrameProperties>(framePropObjs[i], "FrameProperties");

                // Extract ETABS modifiers if provided
                ETABSFrameModifiers etabsModifiers = null;
                if (etabsModObjs.Count > i && etabsModObjs[i] != null)
                {
                    etabsModifiers = ExtractObject<ETABSFrameModifiers>(etabsModObjs[i], "ETABSFrameModifiers");
                }

                if (baseLevel == null || topLevel == null || frameProps == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid level or properties at index {i}");
                    continue;
                }

                Brace brace = new Brace
                {
                    StartPoint = new Point2D(line.FromX * 12, line.FromY * 12),
                    EndPoint = new Point2D(line.ToX * 12, line.ToY * 12),
                    BaseLevelId = baseLevel.Id,
                    TopLevelId = topLevel.Id,
                    FramePropertiesId = frameProps.Id
                };

                // Apply ETABS modifiers if provided
                if (etabsModifiers != null)
                {
                    brace.ETABSModifiers = etabsModifiers;
                }

                braces.Add(new GH_Brace(brace));
            }

            DA.SetDataList(0, braces);
        }

        private T ExtractObject<T>(object obj, string typeName) where T : class
        {
            if (obj is T directType)
                return directType;

            if (obj is GH_ModelGoo<T> ghType)
                return ghType.Value;

            // Try to handle string IDs (for compatibility)
            if (obj is string && typeof(T) == typeof(Level))
            {
                return new Level((string)obj, null, 0) as T;
            }
            else if (obj is string && typeof(T) == typeof(FrameProperties))
            {
                FrameProperties props = new FrameProperties { Name = (string)obj };
                return props as T;
            }

            // Try to cast from IGH_Goo
            if (obj is Grasshopper.Kernel.Types.IGH_Goo goo)
            {
                T result = null;
                if (goo.CastTo(out result))
                    return result;
            }

            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Could not extract {typeName}");
            return null;
        }

        public override Guid ComponentGuid => new Guid("67A2BD5D-1A2F-4F34-89C7-5D8E43C09318");
    }
}
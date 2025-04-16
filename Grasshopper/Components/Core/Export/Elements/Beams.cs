using Grasshopper.Kernel;
using RG = Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.Properties;
using Core.Models.Geometry; 
using Core.Models.ModelLayout;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class BeamCollectorComponent : ComponentBase
    {
        public BeamCollectorComponent()
          : base("Beams", "Beams",
              "Creates beam objects for the structural model",
              "IMEG", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Lines representing beams", GH_ParamAccess.list);
            pManager.AddGenericParameter("Level", "LVL", "Level this beam belongs to", GH_ParamAccess.list);
            pManager.AddGenericParameter("Properties", "P", "Frame properties for this beam", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Is Lateral", "IL", "Is beam part of the lateral system", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Is Joist", "IJ", "Is beam a joist", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beams", "B", "Beam objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<RG.Line> lines = new List<RG.Line>();
            List<object> levelObjs = new List<object>();
            List<object> propObjs = new List<object>();
            List<bool> isLateralList = new List<bool>();
            List<bool> isJoistList = new List<bool>();

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, levelObjs)) return;
            if (!DA.GetDataList(2, propObjs)) return;
            if (!DA.GetDataList(3, isLateralList)) return;
            if (!DA.GetDataList(4, isJoistList)) return;

            // Ensure lists have matching lengths by extending levelObjs with the last item
            if (levelObjs.Count > 0 && levelObjs.Count < lines.Count)
            {
                object lastLevel = levelObjs[levelObjs.Count - 1];
                while (levelObjs.Count < lines.Count)
                {
                    levelObjs.Add(lastLevel);
                }
            }

            // Check for properties list length and extend if needed
            if (propObjs.Count > 0 && propObjs.Count < lines.Count)
            {
                object lastProp = propObjs[propObjs.Count - 1];
                while (propObjs.Count < lines.Count)
                {
                    propObjs.Add(lastProp);
                }
            }

            // Check for lateral flag list length and extend if needed
            if (isLateralList.Count < lines.Count)
            {
                if (isLateralList.Count == 0)
                {
                    isLateralList.Add(false); // Default value if no input provided
                }
                bool lastLateral = isLateralList[isLateralList.Count - 1];
                while (isLateralList.Count < lines.Count)
                {
                    isLateralList.Add(lastLateral);
                }
            }

            // Check for joist flag list length and extend if needed    
            if (isJoistList.Count < lines.Count)
            {
                if (isLateralList.Count == 0)
                {
                    isLateralList.Add(false); // Default value if no input provided
                }
                bool lastLateral = isLateralList[isLateralList.Count - 1];
                while (isLateralList.Count < lines.Count)
                {
                    isLateralList.Add(lastLateral);
                }
            }


            List<GH_Beam> beams = new List<GH_Beam>();
            for (int i = 0; i < lines.Count; i++)
            {
                RG.Line line = lines[i];
                bool isLateral = isLateralList[i];
                bool isJoist = isJoistList[i];  
                Level level = ExtractObject<Level>(levelObjs[i], "Level");
                FrameProperties frameProps = ExtractObject<FrameProperties>(propObjs[i], "FrameProperties");

                if (level == null || frameProps == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid level or properties at index {i}");
                    continue;
                }

                Beam beam = new Beam
                {
                    StartPoint = new Point2D(line.FromX * 12, line.FromY * 12),
                    EndPoint = new Point2D(line.ToX * 12, line.ToY * 12),
                    LevelId = level.Id,
                    FramePropertiesId = frameProps.Id,
                    IsLateral = isLateral,
                    IsJoist = isJoist
                };

                beams.Add(new GH_Beam(beam));
            }

            DA.SetDataList(0, beams);
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

            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Could not extract {typeName}");
            return null;
        }

        public override Guid ComponentGuid => new Guid("C5DD1EF0-3940-47A2-9E1F-A271F3F7D3A4");
    }
}
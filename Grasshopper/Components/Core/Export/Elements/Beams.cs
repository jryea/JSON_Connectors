using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Grasshopper.Utilities;

namespace JSON_Connectors.Components.Core.Export.Elements
{
    public class BeamCollectorComponent : GH_Component
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
            pManager.AddTextParameter("Level ID", "LVL", "ID of the level this beam belongs to", GH_ParamAccess.list);
            pManager.AddTextParameter("Properties ID", "P", "ID of the beam properties", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Is Lateral", "IL", "Is beam part of the lateral system", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Is Joist", "IJ", "Is beam a joist", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beams", "B", "Beam objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Line> lines = new List<Line>();
            List<string> levelIds = new List<string>();
            List<string> propertiesIds = new List<string>();
            bool isLateral = false;
            bool isJoist = false;

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, levelIds)) return;
            if (!DA.GetDataList(2, propertiesIds)) return;
            DA.GetData(3, ref isLateral);
            DA.GetData(4, ref isJoist);

            if (lines.Count != levelIds.Count || lines.Count != propertiesIds.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of lines must match number of level IDs and properties IDs");
                return;
            }

            List<GH_Beam> beams = new List<GH_Beam>();
            for (int i = 0; i < lines.Count; i++)
            {
                Line line = lines[i];
                Beam beam = new Beam
                {
                    StartPoint = new Point2D(line.FromX * 12, line.FromY * 12),
                    EndPoint = new Point2D(line.ToX * 12, line.ToY * 12),
                    LevelId = levelIds[i],
                    PropertiesId = propertiesIds[i],
                    IsLateral = isLateral,
                    IsJoist = isJoist
                };

                beams.Add(new GH_Beam(beam));
            }

            DA.SetDataList(0, beams);
        }

        public override Guid ComponentGuid => new Guid("C5DD1EF0-3940-47A2-9E1F-A271F3F7D3A4");
    }
}
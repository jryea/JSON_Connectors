using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Grasshopper.Utilities;

namespace JSON_Connectors.Components.Core.Export.Elements
{
    public class ColumnCollectorComponent : GH_Component
    {
        public ColumnCollectorComponent()
          : base("Columns", "Columns",
              "Creates column objects for the structural model",
              "IMEG", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Lines representing columns", GH_ParamAccess.list);
            pManager.AddTextParameter("Base Level ID", "BL", "ID of the base level", GH_ParamAccess.list);
            pManager.AddTextParameter("Top Level ID", "TL", "ID of the top level", GH_ParamAccess.list);
            pManager.AddTextParameter("Section ID", "S", "ID of the column section", GH_ParamAccess.list);
            pManager.AddTextParameter("Analysis Type", "A", "Lateral or gravity type", GH_ParamAccess.item, "Lateral");
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Columns", "C", "Column objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Line> lines = new List<Line>();
            List<string> baseLevelIds = new List<string>();
            List<string> topLevelIds = new List<string>();
            List<string> sectionIds = new List<string>();
            string analysisType = "Lateral";

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, baseLevelIds)) return;
            if (!DA.GetDataList(2, topLevelIds)) return;
            if (!DA.GetDataList(3, sectionIds)) return;
            DA.GetData(4, ref analysisType);

            if (lines.Count != baseLevelIds.Count || lines.Count != topLevelIds.Count || lines.Count != sectionIds.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of lines must match number of base/top level IDs and section IDs");
                return;
            }

            List<GH_Column> columns = new List<GH_Column>();
            for (int i = 0; i < lines.Count; i++)
            {
                Line line = lines[i];
                Column column = new Column
                {
                    StartPoint = new Point3D(line.FromX * 12, line.FromY * 12, line.FromZ * 12),
                    EndPoint = new Point3D(line.ToX * 12, line.ToY * 12, line.ToZ * 12),
                    BaseLevelId = baseLevelIds[i],
                    TopLevelId = topLevelIds[i],
                    SectionId = sectionIds[i],
                    Analysis = new Dictionary<string, string>
                    {
                        { "lateralOrGravity", analysisType }
                    }
                };

                columns.Add(new GH_Column(column));
            }

            DA.SetDataList(0, columns);
        }

        public override Guid ComponentGuid => new Guid("1F2A3B4C-5D6E-7F8A-9B0C-1D2E3F4A5B6C");
    }
}
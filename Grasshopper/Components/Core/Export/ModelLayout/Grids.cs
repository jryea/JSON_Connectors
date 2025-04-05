using Grasshopper.Kernel;
using RG = Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.ModelLayout;
using Grasshopper.Utilities;
using Core.Models.Geometry;

namespace Grasshopper.Components.Core.Export.ModelLayout
{
    public class GridCollectorComponent : ComponentBase
    {
        public GridCollectorComponent()
          : base("Grids", "Grids",
              "Creates grid objects for the structural model",
              "IMEG", "Model Layout")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Lines representing grids", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "N", "Names for each grid", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Bubbles", "B", "Show bubbles", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Grids", "G", "Grid objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<RG.Line> lines = new List<RG.Line>();
            List<string> names = new List<string>();
            bool showBubbles = false;

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, names)) return;
            DA.GetData(2, ref showBubbles);

            if (lines.Count != names.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of lines must match number of names");
                return;
            }

            List<GH_Grid> grids = new List<GH_Grid>();
            for (int i = 0; i < lines.Count; i++)
            {
                RG.Line line = lines[i];
                GridPoint startPoint = new GridPoint(line.FromX * 12, line.FromY * 12, 0, showBubbles);
                GridPoint endPoint = new GridPoint(line.ToX * 12, line.ToY * 12, 0, showBubbles);
                Grid grid = new Grid(names[i], startPoint, endPoint);
                grids.Add(new GH_Grid(grid));
            }

            DA.SetDataList(0, grids);
        }

        public override Guid ComponentGuid => new Guid("8b384189-15d3-40c3-8f9b-235b61d956fc");
    }
}
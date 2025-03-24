using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Grasshopper.Utilities;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System.Linq;

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
            pManager.AddLineParameter("Lines", "L", "Lines representing columns", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Base Level", "BL", "Base level of the column", GH_ParamAccess.list);
            pManager.AddGenericParameter("Top Level", "TL", "Top level of the column", GH_ParamAccess.list);
            pManager.AddGenericParameter("Frame Properties", "P", "Frame properties for this column", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Columns", "C", "Column objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Line> linesTree;
            List<object> baseLevelObjs = new List<object>();
            List<object> topLevelObjs = new List<object>();
            List<object> framePropObjs = new List<object>();

            if (!DA.GetDataTree(0, out linesTree)) return;
            if (!DA.GetDataList(1, baseLevelObjs)) return;
            if (!DA.GetDataList(2, topLevelObjs)) return;
            if (!DA.GetDataList(3, framePropObjs)) return;

            // Check that the number of branches matches the number of levels and properties
            if (linesTree.PathCount != baseLevelObjs.Count ||
                linesTree.PathCount != topLevelObjs.Count ||
                linesTree.PathCount != framePropObjs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of line branches ({linesTree.PathCount}) must match number of base levels ({baseLevelObjs.Count}), " +
                    $"top levels ({topLevelObjs.Count}), and properties ({framePropObjs.Count})");
                return;
            }

            List<GH_Column> columns = new List<GH_Column>();

            // Process each branch of the tree
            for (int i = 0; i < linesTree.PathCount; i++)
            {
                GH_Path path = linesTree.Paths[i];
                List<GH_Line> linesBranch = linesTree.get_Branch(path).Cast<GH_Line>().ToList();

                // Get the corresponding level and property for this branch
                Level baseLevel = ExtractObject<Level>(baseLevelObjs[i], "BaseLevel");
                Level topLevel = ExtractObject<Level>(topLevelObjs[i], "TopLevel");
                FrameProperties frameProps = ExtractObject<FrameProperties>(framePropObjs[i], "FrameProperties");

                if (baseLevel == null || topLevel == null || frameProps == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Invalid level or properties at branch index {i}");
                    continue;
                }

                // Process each column in this branch
                foreach (GH_Line ghLine in linesBranch)
                {
                    if (ghLine == null) continue;

                    Line line = ghLine.Value;

                    Column column = new Column
                    {
                        StartPoint = new Point2D(line.FromX * 12, line.FromY * 12),
                        EndPoint = new Point2D(line.ToX * 12, line.ToY * 12),
                        BaseLevel = baseLevel,
                        TopLevel = topLevel,
                        FrameProperties = frameProps
                    };

                    columns.Add(new GH_Column(column));
                }
            }

            DA.SetDataList(0, columns);
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

        public override Guid ComponentGuid => new Guid("1F2A3B4C-5D6E-7F8A-9B0C-1D2E3F4A5B6C");
    }
}
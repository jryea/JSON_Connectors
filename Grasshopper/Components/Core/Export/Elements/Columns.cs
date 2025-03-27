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

namespace Grasshopper.Components.Core.Export.Elements
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
            pManager.AddGenericParameter("Base Level", "BL", "Base level of the column", GH_ParamAccess.list);
            pManager.AddGenericParameter("Top Level", "TL", "Top level of the column", GH_ParamAccess.list);
            pManager.AddGenericParameter("Frame Properties", "P", "Frame properties for this column", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Is Lateral", "IL", "Is this column lateral?", GH_ParamAccess.list);

            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Columns", "C", "Column objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Line> lines = new List<Line>();
            List<object> baseLevelObjs = new List<object>();
            List<object> topLevelObjs = new List<object>();
            List<object> framePropObjs = new List<object>();
            List<bool> isLateral = new List<bool>();

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, baseLevelObjs)) return;
            if (!DA.GetDataList(2, topLevelObjs)) return;
            if (!DA.GetDataList(3, framePropObjs)) return;
            DA.GetDataList(4, isLateral);

            // Ensure isLateral has the correct count and replace null values with false
            for (int i = 0; i < baseLevelObjs.Count; i++)
            {
                if (i >= isLateral.Count)
                {
                    isLateral.Insert(i, false);
                }
            }

            // Check that the number of branches matches the number of levels and properties
            if (lines.Count != baseLevelObjs.Count ||
                lines.Count != topLevelObjs.Count ||
                lines.Count != framePropObjs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of column lines ({lines.Count}) must match number of base levels ({baseLevelObjs.Count}), " +
                    $"top levels ({topLevelObjs.Count}), and properties ({framePropObjs.Count})");
                return;
            }

            List<GH_Column> columns = new List<GH_Column>();

            // Process each column in this branch
            for (int i = 0; i < lines.Count; i++)
            {
                // Get the corresponding level and property for this branch
                FrameProperties frameProps = ExtractObject<FrameProperties>(framePropObjs[i], "FrameProperties");
                Level baseLevel = ExtractObject<Level>(baseLevelObjs[i], "BaseLevel");
                Level topLevel = ExtractObject<Level>(topLevelObjs[i], "TopLevel");
                Line line = lines[i];

                if (baseLevel == null || topLevel == null || frameProps == null || line == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Invalid level or properties at branch index {i}");
                    continue;
                }

                Column column = new Column
                {
                    StartPoint = new Point2D(line.FromX * 12, line.FromY * 12),
                    EndPoint = new Point2D(line.ToX * 12, line.ToY * 12),
                    BaseLevelId = baseLevel.Id,
                    TopLevelId = topLevel.Id,
                    FramePropertiesId = frameProps.Id,
                    IsLateral = isLateral[i],
                };

                columns.Add(new GH_Column(column));
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
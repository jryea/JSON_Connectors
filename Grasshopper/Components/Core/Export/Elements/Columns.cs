using Grasshopper.Kernel;
using RG = Rhino.Geometry;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Geometry; 
using Grasshopper.Utilities;
using System.Linq;
using System.Windows.Forms;
using Core.Models.SoftwareSpecific;
using static Core.Models.Properties.Modifiers;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class ColumnCollectorComponent : ComponentBase
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
            pManager.AddAngleParameter("Orientation", "R", "Rotation angle of the column (degrees)", GH_ParamAccess.list);
            pManager.AddGenericParameter("ETABS Modifiers", "EM", "ETABS-specific frame modifiers", GH_ParamAccess.list);

            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;

        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Columns", "C", "Column objects", GH_ParamAccess.list);
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<RG.Line> lines = new List<RG.Line>();
            List<object> baseLevelObjs = new List<object>();
            List<object> topLevelObjs = new List<object>();
            List<object> framePropObjs = new List<object>();
            List<bool> isLateral = new List<bool>();
            List<double> orientation = new List<double>();
            List<object> etabsModObjs = new List<object>();

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, baseLevelObjs)) return;
            if (!DA.GetDataList(2, topLevelObjs)) return;
            if (!DA.GetDataList(3, framePropObjs)) return;
            DA.GetDataList(4, isLateral);
            DA.GetDataList(5, orientation);
            DA.GetDataList(6, etabsModObjs);

            // Extend base level objects list if needed
            if (baseLevelObjs.Count > 0 && baseLevelObjs.Count < lines.Count)
            {
                object lastBaseLevel = baseLevelObjs[baseLevelObjs.Count - 1];
                while (baseLevelObjs.Count < lines.Count)
                    baseLevelObjs.Add(lastBaseLevel);
            }

            // Extend top level objects list if needed
            if (topLevelObjs.Count > 0 && topLevelObjs.Count < lines.Count)
            {
                object lastTopLevel = topLevelObjs[topLevelObjs.Count - 1];
                while (topLevelObjs.Count < lines.Count)
                    topLevelObjs.Add(lastTopLevel);
            }

            // Extend frame property objects list if needed
            if (framePropObjs.Count > 0 && framePropObjs.Count < lines.Count)
            {
                object lastFrameProp = framePropObjs[framePropObjs.Count - 1];
                while (framePropObjs.Count < lines.Count)
                    framePropObjs.Add(lastFrameProp);
            }

            // Extend isLateral list if needed
            if (isLateral.Count > 0 && isLateral.Count < lines.Count)
            {
                bool lastIsLateral = isLateral[isLateral.Count - 1];
                while (isLateral.Count < lines.Count)
                    isLateral.Add(lastIsLateral);
            }
            else if (isLateral.Count == 0)
            {
                // Default to false for all columns if no value was provided
                isLateral = Enumerable.Repeat(false, lines.Count).ToList();
            }

            // Extend orientation list if needed
            if (orientation.Count > 0 && orientation.Count < lines.Count)
            {
                double lastOrientation = orientation[orientation.Count - 1];
                while (orientation.Count < lines.Count)
                    orientation.Add(lastOrientation);
            }
            else if (orientation.Count == 0)
            {
                // Default to 0 degrees for all columns if no value was provided
                orientation = Enumerable.Repeat(0.0, lines.Count).ToList();
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

            // Check that the number of branches matches after extension
            if (lines.Count != baseLevelObjs.Count ||
                lines.Count != topLevelObjs.Count ||
                lines.Count != framePropObjs.Count ||
                lines.Count != isLateral.Count ||
                lines.Count != orientation.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of column lines ({lines.Count}) must match number of base levels ({baseLevelObjs.Count}), " +
                    $"top levels ({topLevelObjs.Count}), and properties ({framePropObjs.Count})");
                return;
            }

            List<GH_Column> columns = new List<GH_Column>();

            // Process each column in this list
            for (int i = 0; i < lines.Count; i++)
            {
                // Get the corresponding level and property for this branch
                FrameProperties frameProps = ExtractObject<FrameProperties>(framePropObjs[i], "FrameProperties");
                Level baseLevel = ExtractObject<Level>(baseLevelObjs[i], "BaseLevel");
                Level topLevel = ExtractObject<Level>(topLevelObjs[i], "TopLevel");
                RG.Line line = lines[i];

                // Extract ETABS modifiers if provided
                ETABSFrameModifiers etabsModifiers = null;
                if (etabsModObjs.Count > i && etabsModObjs[i] != null)
                {
                    etabsModifiers = ExtractObject<ETABSFrameModifiers>(etabsModObjs[i], "ETABSFrameModifiers");
                }

                if (baseLevel == null || topLevel == null || frameProps == null || line == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Invalid level or properties at list index {i}");
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
                    Orientation = orientation[i]
                };

                // Apply ETABS modifiers if provided
                if (etabsModifiers != null)
                {
                    column.ETABSModifiers = etabsModifiers;
                }

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
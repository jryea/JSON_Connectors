using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Core.Models.Elements;
using Core.Models.ModelLayout;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class ColumnCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ColumnCollector class.
        /// </summary>
        public ColumnCollectorComponent()
          : base("Columns", "Columns",
              "Creates column objects that can be used in the structural model",
              "IMEG", "Elements")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Lines representing columns", GH_ParamAccess.list);
            pManager.AddTextParameter("Base Level ID", "BL", "ID of the base level", GH_ParamAccess.list);
            pManager.AddTextParameter("Top Level ID", "TL", "ID of the top level", GH_ParamAccess.list);
            pManager.AddTextParameter("Section ID", "S", "ID of the column section", GH_ParamAccess.list);
            pManager.AddTextParameter("Analysis Type", "A", "Lateral or gravity type (optional)", GH_ParamAccess.list, "Lateral");

            // Make some parameters optional
            pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Columns", "C", "Column objects for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<Line> lines = new List<Line>();
            List<string> baseLevelIds = new List<string>();
            List<string> topLevelIds = new List<string>();
            List<string> sectionIds = new List<string>();
            List<string> analysisTypes = new List<string>();

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, baseLevelIds)) return;
            if (!DA.GetDataList(2, topLevelIds)) return;
            if (!DA.GetDataList(3, sectionIds)) return;
            DA.GetDataList(4, analysisTypes); // Optional

            // Basic validation
            if (lines.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No column lines provided");
                return;
            }

            if (lines.Count != baseLevelIds.Count || lines.Count != topLevelIds.Count || lines.Count != sectionIds.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of lines ({lines.Count}) must match number of base level IDs ({baseLevelIds.Count}), " +
                    $"top level IDs ({topLevelIds.Count}), and section IDs ({sectionIds.Count})");
                return;
            }

            // Ensure optional analysis types list has the right size or fill with defaults
            if (analysisTypes.Count > 0 && analysisTypes.Count != lines.Count)
            {
                if (analysisTypes.Count == 1)
                {
                    // Use the single value for all columns
                    string analysisType = analysisTypes[0];
                    analysisTypes.Clear();
                    for (int i = 0; i < lines.Count; i++)
                        analysisTypes.Add(analysisType);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Number of analysis types ({analysisTypes.Count}) must match number of lines ({lines.Count}) or be a single value");
                    return;
                }
            }
            else if (analysisTypes.Count == 0)
            {
                // Fill with default values
                for (int i = 0; i < lines.Count; i++)
                    analysisTypes.Add("Lateral");
            }

            try
            {
                // Create columns
                List<Column> columns = new List<Column>();

                for (int i = 0; i < lines.Count; i++)
                {
                    Line line = lines[i];

                    // Create a new column
                    Column column = new Column();

                    // Set start and end points (converting to inches if Rhino is in feet)
                    column.StartPoint = new Point3D(
                        line.FromX * 12,
                        line.FromY * 12,
                        line.FromZ * 12);

                    column.EndPoint = new Point3D(
                        line.ToX * 12,
                        line.ToY * 12,
                        line.ToZ * 12);

                    // Set the column properties
                    column.BaseLevelId = baseLevelIds[i];
                    column.TopLevelId = topLevelIds[i];
                    column.SectionId = sectionIds[i];

                    // Set analysis properties
                    column.Analysis = new Dictionary<string, string>
                    {
                        { "lateralOrGravity", analysisTypes[i] }
                    };

                    columns.Add(column);
                }

                // Set output
                DA.SetDataList(0, columns);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("1F2A3B4C-5D6E-7F8A-9B0C-1D2E3F4A5B6C");
    }
}
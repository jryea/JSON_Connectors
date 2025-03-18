using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Core.Models.Model;
using Core.Models.Elements;

namespace Grasshopper.Export
{
    public class GridCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GridCollector class.
        /// </summary>
        public GridCollectorComponent()
          : base("Grid Collector", "GridCollect",
              "Creates grid objects from lines that can be used in the structural model",
              "Structural", "Elements")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Lines representing grids", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "N", "Names for each grid", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Grids", "G", "Grid objects for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<Line> lines = new List<Line>();
            List<string> names = new List<string>();

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, names)) return;

            // Basic validation
            if (lines.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No grid lines provided");
                return;
            }

            if (lines.Count != names.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of grid lines ({lines.Count}) does not match number of names ({names.Count})");
                return;
            }

            try
            {
                // Create grids
                List<Grid> grids = new List<Grid>();
                for (int i = 0; i < lines.Count; i++)
                {
                    Line line = lines[i];
                    string name = names[i];
                    bool isBubble = false;

                    GridPoint startPoint = new GridPoint(
                        line.FromX, line.FromY, 0, isBubble);

                    GridPoint endPoint = new GridPoint(
                        line.ToX, line.ToY, 0, isBubble);

                    Grid grid = new Grid(name, startPoint, endPoint);
                    grids.Add(grid);
                }

                // Set output
                DA.SetDataList(0, grids);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("8b384189-15d3-40c3-8f9b-235b61d956fc");
    }
}
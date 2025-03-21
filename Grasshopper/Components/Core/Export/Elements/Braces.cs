using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Core.Models.Elements;
using Core.Models.ModelLayout;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class BraceCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BraceCollector class.
        /// </summary>
        public BraceCollectorComponent()
          : base("Braces", "Braces",
              "Creates brace objects that can be used in the structural model",
              "IMEG", "Elements")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Lines representing braces", GH_ParamAccess.list);
            pManager.AddTextParameter("Material ID", "M", "ID of the brace material", GH_ParamAccess.list);
            pManager.AddTextParameter("Section ID", "S", "ID of the brace section", GH_ParamAccess.list);
            pManager.AddTextParameter("Level ID", "LVL", "ID of the level this brace belongs to", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Braces", "B", "Brace objects for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<Line> lines = new List<Line>();
            List<string> materialIds = new List<string>();
            List<string> sectionIds = new List<string>();
            List<string> levelIds = new List<string>();

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, materialIds)) return;
            if (!DA.GetDataList(2, sectionIds)) return;
            if (!DA.GetDataList(3, levelIds)) return;

            // Basic validation
            if (lines.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No brace lines provided");
                return;
            }

            if (lines.Count != materialIds.Count || lines.Count != sectionIds.Count || lines.Count != levelIds.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of lines ({lines.Count}) must match number of material IDs ({materialIds.Count}), " +
                    $"section IDs ({sectionIds.Count}), and level IDs ({levelIds.Count})");
                return;
            }

            try
            {
                // Create braces
                List<Brace> braces = new List<Brace>();

                for (int i = 0; i < lines.Count; i++)
                {
                    Line line = lines[i];

                    // Create a new brace
                    Brace brace = new Brace();

                    // Set start and end points (converting to inches if Rhino is in feet)
                    brace.StartPoint = new Point3D(
                        line.FromX * 12,
                        line.FromY * 12,
                        line.FromZ * 12);

                    brace.EndPoint = new Point3D(
                        line.ToX * 12,
                        line.ToY * 12,
                        line.ToZ * 12);

                    // Set the brace properties
                    brace.MaterialId = materialIds[i];

                    // For section ID we need to handle the structure shown in JSON schema
                    // where sectionId is an object with "analysis" property
                    brace.SectionId = new Dictionary<string, string>
                    {
                        { "analysis", sectionIds[i] }
                    };

                    brace.LevelId = levelIds[i];

                    braces.Add(brace);
                }

                // Set output
                DA.SetDataList(0, braces);
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
        public override Guid ComponentGuid => new Guid("67A2BD5D-1A2F-4F34-89C7-5D8E43C09318");
    }
}
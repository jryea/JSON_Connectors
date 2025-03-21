using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Elements;

namespace Grasshopper.Export.Elements
{
    public class ElementContainerComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ElementContainer component.
        /// </summary>
        public ElementContainerComponent()
          : base("Element Container", "Elements",
              "Collects all structural elements into a single container",
              "IMEG", "Elements")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Floors", "F", "Floor elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Walls", "W", "Wall elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Beams", "B", "Beam elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Braces", "BR", "Brace elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Columns", "C", "Column elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Isolated Footings", "IF", "Isolated footing elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Continuous Footings", "CF", "Continuous footing elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Drilled Piers", "DP", "Drilled pier elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Piles", "P", "Pile elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Piers", "PI", "Pier elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Joints", "J", "Joint elements", GH_ParamAccess.list);

            // Make all parameters optional
            for (int i = 0; i < 11; i++)
            {
                pManager[i].Optional = true;
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Elements", "E", "Container with all structural elements", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Lists to hold elements
            List<Floor> floors = new List<Floor>();
            List<Wall> walls = new List<Wall>();
            List<Beam> beams = new List<Beam>();
            List<Brace> braces = new List<Brace>();
            List<Column> columns = new List<Column>();
            List<IsolatedFooting> isolatedFootings = new List<IsolatedFooting>();
            List<ContinuousFooting> continuousFootings = new List<ContinuousFooting>();
            List<DrilledPier> drilledPiers = new List<DrilledPier>();
            List<Pier> piers = new List<Pier>();
            List<Pile> piles = new List<Pile>();
            List<Joint> joints = new List<Joint>();

            // Try to get all data
            DA.GetDataList(0, floors);
            DA.GetDataList(1, walls);
            DA.GetDataList(2, beams);
            DA.GetDataList(3, braces);
            DA.GetDataList(4, columns);
            DA.GetDataList(5, isolatedFootings);
            DA.GetDataList(6, continuousFootings);
            DA.GetDataList(7, drilledPiers);
            DA.GetDataList(8, piers);
            DA.GetDataList(9, piles);
            DA.GetDataList(10, joints);

            try
            {
                // Create element container
                ElementContainer container = new ElementContainer
                {
                    Floors = floors,
                    Walls = walls,
                    Beams = beams,
                    Braces = braces,
                    Columns = columns,
                    IsolatedFootings = isolatedFootings,
                    Joints = joints,
                    ContinuousFootings = continuousFootings,
                    Piles = piles,
                    Piers = piers,
                    DrilledPiers = drilledPiers
                };

                // Generate summary for feedback
                int totalElements = floors.Count + walls.Count + beams.Count + braces.Count +
                                   columns.Count + isolatedFootings.Count + joints.Count +
                                   continuousFootings.Count + piles.Count + piers.Count +
                                   drilledPiers.Count;

                string summary = $"Element container created with {totalElements} total elements: " +
                                $"{floors.Count} floors, {walls.Count} walls, {beams.Count} beams, " +
                                $"{braces.Count} braces, {columns.Count} columns, " +
                                $"{isolatedFootings.Count} isolated footings, {joints.Count} joints, " +
                                $"{continuousFootings.Count} continuous footings, {piles.Count} piles, " +
                                $"{piers.Count} piers, {drilledPiers.Count} drilled piers";

                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, summary);

                // Set output
                DA.SetData(0, container);
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
        public override Guid ComponentGuid => new Guid("B9A8C7D6-E5F4-3210-1A2B-3C4D5E6F7A8B");
    }
}
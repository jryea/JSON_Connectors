using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class ElementContainerComponent : GH_Component
    {
        public ElementContainerComponent()
          : base("Element Container", "Elements",
              "Collects all structural elements into a single container",
              "IMEG", "Elements")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Floors", "F", "Floor elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Walls", "W", "Wall elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Beams", "B", "Beam elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Braces", "BR", "Brace elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Columns", "C", "Column elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Isolated Footings", "IF", "Isolated footing elements", GH_ParamAccess.list);
            pManager.AddGenericParameter("Joints", "J", "Joint elements", GH_ParamAccess.list);

            for (int i = 0; i < 7; i++)
                pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Elements", "E", "Container with all structural elements", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<object> floorObjects = new List<object>();
            List<object> wallObjects = new List<object>();
            List<object> beamObjects = new List<object>();
            List<object> braceObjects = new List<object>();
            List<object> columnObjects = new List<object>();
            List<object> footingObjects = new List<object>();
            List<object> jointObjects = new List<object>();

            DA.GetDataList(0, floorObjects);
            DA.GetDataList(1, wallObjects);
            DA.GetDataList(2, beamObjects);
            DA.GetDataList(3, braceObjects);
            DA.GetDataList(4, columnObjects);
            DA.GetDataList(5, footingObjects);
            DA.GetDataList(6, jointObjects);

            try
            {
                ElementContainer container = new ElementContainer();

                // Extract floors
                ExtractElements(floorObjects, container.Floors, "Floor");

                // Extract walls
                ExtractElements(wallObjects, container.Walls, "Wall");

                // Extract beams
                ExtractElements(beamObjects, container.Beams, "Beam");

                // Extract braces
                ExtractElements(braceObjects, container.Braces, "Brace");

                // Extract columns
                ExtractElements(columnObjects, container.Columns, "Column");

                // Extract isolated footings
                ExtractElements(footingObjects, container.IsolatedFootings, "IsolatedFooting");

                // Extract joints
                ExtractElements(jointObjects, container.Joints, "Joint");

                int totalElements = container.Floors.Count + container.Walls.Count +
                    container.Beams.Count + container.Braces.Count +
                    container.Columns.Count + container.IsolatedFootings.Count +
                    container.Joints.Count;

                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    $"Added {totalElements} elements to container");

                DA.SetData(0, new GH_ElementContainer(container));
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private void ExtractElements<T>(List<object> objects, List<T> targetList, string typeName) where T : class
        {
            foreach (object obj in objects)
            {
                // Check if it's our Goo wrapper
                if (obj is GH_ModelGoo<T> ghObj)
                {
                    targetList.Add(ghObj.Value);
                }
                // Direct type
                else if (obj is T element)
                {
                    targetList.Add(element);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Skipped object that is not a valid {typeName}");
                }
            }
        }

        public override Guid ComponentGuid => new Guid("B9A8C7D6-E5F4-3210-1A2B-3C4D5E6F7A8B");
    }
}
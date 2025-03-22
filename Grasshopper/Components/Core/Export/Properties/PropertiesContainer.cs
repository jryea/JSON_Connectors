using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Grasshopper.Utilities;

namespace JSON_Connectors.Components.Core.Export.Properties
{
    public class PropertiesContainerComponent : GH_Component
    {
        public PropertiesContainerComponent()
          : base("Properties Container", "PropsCont",
              "Collects all property definitions into a container",
              "IMEG", "Properties")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Materials", "M", "Material definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Wall Properties", "WP", "Wall property definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Floor Properties", "FP", "Floor property definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Diaphragms", "D", "Diaphragm definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Frame Properties", "FRP", "Frame property definitions", GH_ParamAccess.list);

            for (int i = 0; i < 5; i++)
                pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Properties", "P", "Properties container", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<object> materialObjects = new List<object>();
            List<object> wallPropObjects = new List<object>();
            List<object> floorPropObjects = new List<object>();
            List<object> diaphragmObjects = new List<object>();
            List<object> framePropObjects = new List<object>();

            DA.GetDataList(0, materialObjects);
            DA.GetDataList(1, wallPropObjects);
            DA.GetDataList(2, floorPropObjects);
            DA.GetDataList(3, diaphragmObjects);
            DA.GetDataList(4, framePropObjects);

            try
            {
                PropertiesContainer container = new PropertiesContainer();

                // Extract materials
                ExtractProperties(materialObjects, container.Materials, "Material");

                // Extract wall properties
                ExtractProperties(wallPropObjects, container.WallProperties, "WallProperties");

                // Extract floor properties
                ExtractProperties(floorPropObjects, container.FloorProperties, "FloorProperties");

                // Extract diaphragms
                ExtractProperties(diaphragmObjects, container.Diaphragms, "Diaphragm");

                // Extract frame properties
                ExtractProperties(framePropObjects, container.FrameProperties, "FrameProperties");

                int totalProps = container.Materials.Count + container.WallProperties.Count +
                    container.FloorProperties.Count + container.Diaphragms.Count +
                    container.FrameProperties.Count;

                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    $"Added {totalProps} property definitions to container");

                DA.SetData(0, new GH_PropertiesContainer(container));
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private void ExtractProperties<T>(List<object> objects, List<T> targetList, string typeName) where T : class
        {
            foreach (object obj in objects)
            {
                // Check if it's our Goo wrapper
                if (obj is GH_ModelGoo<T> ghObj)
                {
                    targetList.Add(ghObj.Value);
                }
                // Direct type
                else if (obj is T prop)
                {
                    targetList.Add(prop);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Skipped object that is not a valid {typeName}");
                }
            }
        }

        public override Guid ComponentGuid => new Guid("D3C2B1A0-9F8E-7D6C-5B4A-3E2F1D0C9B8A");
    }
}
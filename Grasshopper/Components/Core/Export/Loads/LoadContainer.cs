using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using CM = Core.Models.Loads;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.Loads
{
    public class LoadContainerComponent : ComponentBase
    {
        public LoadContainerComponent()
          : base("Load Container", "Loads",
              "Creates a load container with load definitions, surface loads, and load combinations",
              "IMEG", "Loads")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("LoadDefinitions", "LD", "Load definition objects", GH_ParamAccess.list);
            pManager.AddGenericParameter("SurfaceLoads", "SL", "Surface load objects", GH_ParamAccess.list);
            pManager.AddGenericParameter("LoadCombinations", "LC", "Load combination objects", GH_ParamAccess.list);

            // Set optional parameters
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Loads", "L", "Load container", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<object> loadDefinitionObjects = new List<object>();
            List<object> surfaceLoadObjects = new List<object>();
            List<object> loadCombinationObjects = new List<object>();

            DA.GetDataList(0, loadDefinitionObjects);
            DA.GetDataList(1, surfaceLoadObjects);
            DA.GetDataList(2, loadCombinationObjects);

            try
            {
                CM.LoadContainer loadContainer = new CM.LoadContainer();

                // Process load definitions
                foreach (object obj in loadDefinitionObjects)
                {
                    if (obj is GH_LoadDefinition ghLoadDef)
                    {
                        loadContainer.LoadDefinitions.Add(ghLoadDef.Value);
                    }
                    else if (obj is CM.LoadDefinition loadDef)
                    {
                        loadContainer.LoadDefinitions.Add(loadDef);
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "One or more load definition objects are not of type LoadDefinition");
                    }
                }

                // Process surface loads
                foreach (object obj in surfaceLoadObjects)
                {
                    if (obj is GH_SurfaceLoad ghSurfaceLoad)
                    {
                        loadContainer.SurfaceLoads.Add(ghSurfaceLoad.Value);
                    }
                    else if (obj is CM.SurfaceLoad surfaceLoad)
                    {
                        loadContainer.SurfaceLoads.Add(surfaceLoad);
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "One or more surface load objects are not of type SurfaceLoad");
                    }
                }

                // Process load combinations
                foreach (object obj in loadCombinationObjects)
                {
                    if (obj is GH_LoadCombination ghLoadCombo)
                    {
                        loadContainer.LoadCombinations.Add(ghLoadCombo.Value);
                    }
                    else if (obj is CM.LoadCombination loadCombo)
                    {
                        loadContainer.LoadCombinations.Add(loadCombo);
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "One or more load combination objects are not of type LoadCombination");
                    }
                }

                // Output the container wrapped in a Goo object
                DA.SetData(0, new GH_LoadContainer(loadContainer));
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        public override Guid ComponentGuid => new Guid("8A7B6C5D-9E8F-4D3C-B2A1-0F1E2D3C4B5A");
    }
}
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using Core.Models.ModelLayout;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.ModelLayout
{
    public class LayoutContainerComponent : ComponentBase
    {
        public LayoutContainerComponent()
          : base("Model Layout", "Layout",
              "Creates a model layout container with grids, levels, and floor types",
              "IMEG", "Model Layout")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Grids", "G", "Grid objects", GH_ParamAccess.list);
            pManager.AddGenericParameter("Levels", "L", "Level objects", GH_ParamAccess.list);
            pManager.AddGenericParameter("FloorTypes", "FT", "Floor type objects", GH_ParamAccess.list);

            // Set optional parameters
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Layout", "L", "Model layout container", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<object> gridObjects = new List<object>();
            List<object> levelObjects = new List<object>();
            List<object> floorTypeObjects = new List<object>();

            DA.GetDataList(0, gridObjects);
            DA.GetDataList(1, levelObjects);
            DA.GetDataList(2, floorTypeObjects);

            try
            {
                ModelLayoutContainer modelLayout = new ModelLayoutContainer();

                // Process grids
                foreach (object obj in gridObjects)
                {
                    if (obj is GH_Grid ghGrid)
                    {
                        modelLayout.Grids.Add(ghGrid.Value);
                    }
                    else if (obj is Grid grid)
                    {
                        modelLayout.Grids.Add(grid);
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "One or more grid objects are not of type Grid");
                    }
                }

                // Process floor types
                foreach (object obj in floorTypeObjects)
                {
                    if (obj is GH_FloorType ghFloorType)
                    {
                        modelLayout.FloorTypes.Add(ghFloorType.Value);
                    }
                    else if (obj is FloorType floorType)
                    {
                        modelLayout.FloorTypes.Add(floorType);
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "One or more floor type objects are not of type FloorType");
                    }
                }

                // Process levels
                foreach (object obj in levelObjects)
                {
                    if (obj is GH_Level ghLevel)
                    {
                        modelLayout.Levels.Add(ghLevel.Value);
                    }
                    else if (obj is Level level)
                    {
                        modelLayout.Levels.Add(level);
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "One or more level objects are not of type Level");
                    }
                }

                // Output the container wrapped in a Goo object
                DA.SetData(0, new GH_ModelLayoutContainer(modelLayout));
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        public override Guid ComponentGuid => new Guid("7F8E9D0C-1B2A-3C4D-5E6F-7A8B9C0D1E2F");
    }
}
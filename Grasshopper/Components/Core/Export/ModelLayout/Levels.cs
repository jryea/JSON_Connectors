using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using Core.Models.ModelLayout;
using Grasshopper.Utilities;

namespace Grasshopper.Components.Core.Export.ModelLayout
{
    public class LevelCollectorComponent : ComponentBase
    {
        public LevelCollectorComponent()
          : base("Levels", "Levels",
              "Creates level objects for the structural model",
              "IMEG", "Model Layout")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each level", GH_ParamAccess.list);
            pManager.AddNumberParameter("Elevations", "E", "Elevation of each level", GH_ParamAccess.list);
            pManager.AddGenericParameter("FloorTypes", "FT", "Floor types from Floor Type component", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Levels", "L", "Level objects", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> names = new List<string>();
            List<double> elevations = new List<double>();
            List<object> floorTypeObjects = new List<object>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, elevations)) return;
            if (!DA.GetDataList(2, floorTypeObjects)) return;

            if (names.Count != elevations.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Number of names and elevations must match");
                return;
            }

            // Extend floorTypeObjects list with the last value until it matches the length of names and elevations
            if (floorTypeObjects.Count > 0)
            {
                object lastFloorType = floorTypeObjects[floorTypeObjects.Count - 1];
                while (floorTypeObjects.Count < names.Count)
                {
                    floorTypeObjects.Add(lastFloorType);
                }
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "FloorTypes list cannot be empty");
                return;
            }

            List<GH_Level> levels = new List<GH_Level>();
            for (int i = 0; i < names.Count; i++)
            {
                FloorType floorType = null;

                // Extract floor type from wrapper if needed
                if (floorTypeObjects[i] is GH_FloorType ghFloorType)
                {
                    floorType = ghFloorType.Value;
                }
                else if (floorTypeObjects[i] is FloorType directFloorType)
                {
                    floorType = directFloorType;
                }

                if (floorType != null)
                {
                    double elevationInches = elevations[i] * 12;
                    Level level = new Level(names[i], floorType.Id, elevationInches);
                    levels.Add(new GH_Level(level));
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        "Input contains object that is not a valid FloorType");
                    return;
                }
            }

            DA.SetDataList(0, levels);
        }


        public override Guid ComponentGuid => new Guid("f9a0b1c2-d3e4-5f6a-7b8c-9d0e1f2a3b4c");
    }
}
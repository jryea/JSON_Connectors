using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Elements;
using Core.Models.Model;
using Core.Models.Properties;

namespace Grasshopper.Export
{
    public class LevelCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the LevelCollector class.
        /// </summary>
        public LevelCollectorComponent()
          : base("Level Collector", "LevelCollect",
              "Creates level objects that can be used in the structural model",
              "Structural", "Elements")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each level", GH_ParamAccess.list);
            pManager.AddNumberParameter("Elevations", "E", "Elevation of each level (in model units)", GH_ParamAccess.list);
            pManager.AddTextParameter("FloorType", "F", "Floor Type (optional)", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Levels", "L", "Level objects for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<string> names = new List<string>();
            List<double> elevations = new List<double>();
            List<string> floorTypes = new List<string>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, elevations)) return;
            if (!DA.GetDataList(2, floorTypes)) return;

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No level names provided");
                return;
            }

            if (names.Count != elevations.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of level names ({names.Count}) does not match number of elevations ({elevations.Count})");
                return;
            }

            if (names.Count != floorTypes.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of floor types ({names.Count}) does not match number of elevations ({elevations.Count})");
                return;
            }

            try
            {
                // Create levels
                List<Level> levels = new List<Level>();
                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    double elevation = elevations[i];
                    string floorType = floorTypes[i];

                    Level level = new Level(name, floorType, elevation);
                    levels.Add(level);
                }

                // Set output
                DA.SetDataList(0, levels);
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
                // You can add custom icon here
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("f9a0b1c2-d3e4-5f6a-7b8c-9d0e1f2a3b4c"); }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Core.Models.Elements;
using Core.Models.ModelLayout;

namespace Grasshopper.Components.Core.Export.Elements
{
    public class BeamCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamCollector class.
        /// </summary>
        public BeamCollectorComponent()
          : base("Beams", "Beams",
              "Creates beam objects that can be used in the structural model",
              "IMEG", "Elements")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Lines representing beams", GH_ParamAccess.list);
            pManager.AddTextParameter("Level ID", "LVL", "ID of the level this beam belongs to", GH_ParamAccess.list);
            pManager.AddTextParameter("Properties ID", "P", "ID of the beam properties", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Is Lateral", "IL", "Is beam part of the lateral system", GH_ParamAccess.list, false);
            pManager.AddBooleanParameter("Is Joist", "IJ", "Is beam a joist", GH_ParamAccess.list, false);

            // Set default values
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beams", "B", "Beam objects for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<Line> lines = new List<Line>();
            List<string> levelIds = new List<string>();
            List<string> propertiesIds = new List<string>();
            List<bool> isLateralFlags = new List<bool>();
            List<bool> isJoistFlags = new List<bool>();

            if (!DA.GetDataList(0, lines)) return;
            if (!DA.GetDataList(1, levelIds)) return;
            if (!DA.GetDataList(2, propertiesIds)) return;
            DA.GetDataList(3, isLateralFlags); // Optional
            DA.GetDataList(4, isJoistFlags); // Optional

            // Basic validation
            if (lines.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No beam lines provided");
                return;
            }

            if (lines.Count != levelIds.Count || lines.Count != propertiesIds.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of lines ({lines.Count}) must match number of level IDs ({levelIds.Count}) and properties IDs ({propertiesIds.Count})");
                return;
            }

            // Ensure optional boolean lists have the right size or are filled with defaults
            if (isLateralFlags.Count > 0 && isLateralFlags.Count != lines.Count)
            {
                if (isLateralFlags.Count == 1)
                {
                    // Use the single value for all beams
                    bool isLateral = isLateralFlags[0];
                    isLateralFlags.Clear();
                    for (int i = 0; i < lines.Count; i++)
                        isLateralFlags.Add(isLateral);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Number of 'Is Lateral' flags ({isLateralFlags.Count}) must match number of lines ({lines.Count}) or be a single value");
                    return;
                }
            }
            else if (isLateralFlags.Count == 0)
            {
                // Fill with default values
                for (int i = 0; i < lines.Count; i++)
                    isLateralFlags.Add(false);
            }

            if (isJoistFlags.Count > 0 && isJoistFlags.Count != lines.Count)
            {
                if (isJoistFlags.Count == 1)
                {
                    // Use the single value for all beams
                    bool isJoist = isJoistFlags[0];
                    isJoistFlags.Clear();
                    for (int i = 0; i < lines.Count; i++)
                        isJoistFlags.Add(isJoist);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Number of 'Is Joist' flags ({isJoistFlags.Count}) must match number of lines ({lines.Count}) or be a single value");
                    return;
                }
            }
            else if (isJoistFlags.Count == 0)
            {
                // Fill with default values
                for (int i = 0; i < lines.Count; i++)
                    isJoistFlags.Add(false);
            }

            try
            {
                // Create beams
                List<Beam> beams = new List<Beam>();

                for (int i = 0; i < lines.Count; i++)
                {
                    Line line = lines[i];

                    // Create a new beam
                    Beam beam = new Beam();

                    // Set start and end points (converting to inches if Rhino is in feet)
                    beam.StartPoint = new Point2D(line.FromX * 12, line.FromY * 12);
                    beam.EndPoint = new Point2D(line.ToX * 12, line.ToY * 12);

                    // Set the beam properties
                    beam.LevelId = levelIds[i];
                    beam.PropertiesId = propertiesIds[i];
                    beam.IsLateral = isLateralFlags[i];
                    beam.IsJoist = isJoistFlags[i];

                    beams.Add(beam);
                }

                // Set output
                DA.SetDataList(0, beams);
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
        public override Guid ComponentGuid => new Guid("C5DD1EF0-3940-47A2-9E1F-A271F3F7D3A4");
    }
}
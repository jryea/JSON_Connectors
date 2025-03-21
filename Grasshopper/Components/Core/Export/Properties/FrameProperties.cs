using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;

namespace Grasshopper.Export
{
    public class FramePropertiesCollectorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FramePropertiesCollector class.
        /// </summary>
        public FramePropertiesCollectorComponent()
          : base("Frame Properties", "FrameProps",
              "Creates frame property definitions for beams, columns, and braces",
              "IMEG", "Properties")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Names for each frame property", GH_ParamAccess.list);
            pManager.AddTextParameter("Material IDs", "M", "Material IDs for each frame property", GH_ParamAccess.list);
            pManager.AddTextParameter("Shapes", "S", "Shapes for each frame property (e.g., 'W', 'HSS', 'Pipe', 'Custom')", GH_ParamAccess.list);
            pManager.AddNumberParameter("Depths", "D", "Depths (height) for each frame property (in inches)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Widths", "W", "Widths for each frame property (in inches)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Modifiers", "MOD", "Optional stiffness modifiers (0.0-1.0)", GH_ParamAccess.list);

            // Make some parameters optional
            pManager[5].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Frame Properties", "FP", "Frame property definitions for the structural model", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<string> names = new List<string>();
            List<string> materialIds = new List<string>();
            List<string> shapes = new List<string>();
            List<double> depths = new List<double>();
            List<double> widths = new List<double>();
            List<double> modifiers = new List<double>();

            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetDataList(1, materialIds)) return;
            if (!DA.GetDataList(2, shapes)) return;
            if (!DA.GetDataList(3, depths)) return;
            if (!DA.GetDataList(4, widths)) return;
            DA.GetDataList(5, modifiers); // Optional

            // Basic validation
            if (names.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No frame property names provided");
                return;
            }

            if (names.Count != materialIds.Count || names.Count != shapes.Count ||
                names.Count != depths.Count || names.Count != widths.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Number of names ({names.Count}) must match number of material IDs ({materialIds.Count}), " +
                    $"shapes ({shapes.Count}), depths ({depths.Count}), and widths ({widths.Count})");
                return;
            }

            // Ensure optional modifiers have the right size or are empty
            if (modifiers.Count > 0 && modifiers.Count != names.Count)
            {
                if (modifiers.Count == 1)
                {
                    // Use the single value for all properties
                    double modifier = modifiers[0];
                    modifiers.Clear();
                    for (int i = 0; i < names.Count; i++)
                        modifiers.Add(modifier);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Number of modifiers ({modifiers.Count}) must match number of names ({names.Count}) or be a single value");
                    return;
                }
            }

            try
            {
                // Create frame properties
                List<FrameProperties> framePropertiesList = new List<FrameProperties>();

                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    string materialId = materialIds[i];
                    string shape = shapes[i];
                    double depth = depths[i];
                    double width = widths[i];

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(materialId) || string.IsNullOrWhiteSpace(shape))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Empty frame property name, material ID, or shape skipped");
                        continue;
                    }

                    // Validate dimensions
                    if (depth <= 0 || width <= 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"Invalid dimensions (depth: {depth}, width: {width}) for frame property '{name}'. Must be greater than zero.");
                        continue;
                    }

                    // Create a new frame property
                    FrameProperties frameProperties = new FrameProperties
                    {
                        Name = name,
                        MaterialId = materialId,
                        Shape = shape
                    };

                    // Set dimensions
                    frameProperties.Dimensions["depth"] = depth;
                    frameProperties.Dimensions["width"] = width;

                    // Add more dimensions based on shape type
                    if (shape.StartsWith("W", StringComparison.OrdinalIgnoreCase) ||
                        shape.StartsWith("S", StringComparison.OrdinalIgnoreCase) ||
                        shape.StartsWith("HP", StringComparison.OrdinalIgnoreCase))
                    {
                        // For wide flange shapes, add default web and flange thickness
                        frameProperties.Dimensions["webThickness"] = 0.375; // Default web thickness in inches
                        frameProperties.Dimensions["flangeThickness"] = 0.625; // Default flange thickness in inches
                    }
                    else if (shape.StartsWith("HSS", StringComparison.OrdinalIgnoreCase))
                    {
                        // For hollow structural sections, add default wall thickness
                        frameProperties.Dimensions["wallThickness"] = 0.25; // Default wall thickness in inches
                    }
                    else if (shape.StartsWith("Pipe", StringComparison.OrdinalIgnoreCase))
                    {
                        // For pipes, adjust dimensions
                        frameProperties.Dimensions["outerDiameter"] = width;
                        frameProperties.Dimensions["wallThickness"] = 0.25; // Default wall thickness in inches
                    }

                    // Set modifiers if provided
                    if (modifiers.Count > i)
                    {
                        double modifier = modifiers[i];
                        if (modifier >= 0 && modifier <= 1.0)
                        {
                            frameProperties.Modifiers["axial"] = modifier;
                            frameProperties.Modifiers["shear"] = modifier;
                            frameProperties.Modifiers["flexural"] = modifier;
                            frameProperties.Modifiers["torsional"] = modifier;
                        }
                        else
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                                $"Invalid modifier value ({modifier}) for frame property '{name}'. Must be between 0.0 and 1.0.");
                        }
                    }

                    framePropertiesList.Add(frameProperties);
                }

                // Set output
                DA.SetDataList(0, framePropertiesList);
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
        public override Guid ComponentGuid => new Guid("D1E2F3A4-B5C6-D7E8-F9A0-B1C2D3E4F5A6");
    }
}
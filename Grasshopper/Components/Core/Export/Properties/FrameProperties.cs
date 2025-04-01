using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;

namespace Grasshopper.Components.Core.Export.Properties
{
    public class FramePropertiesCollectorComponent : ComponentBase
    {
        // Initializes a new instance of the FramePropertiesCollector class.
        public FramePropertiesCollectorComponent()
          : base("Frame Properties", "FrameProp",
              "Creates frame property definitions for beams, columns, and braces",
              "IMEG", "Properties")
        {
        }

        // Registers all the input parameters for this component.
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name for the frame property", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material", "M", "Material for the frame property", GH_ParamAccess.item);
            pManager.AddTextParameter("Shape", "S", "Shape for the frame property (e.g., 'W', 'HSS', 'Pipe', 'Custom')", GH_ParamAccess.item);
            pManager.AddNumberParameter("Depth", "D", "Depth (height) for the frame property (in inches)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Width", "W", "Width for the frame property (in inches)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Modifier", "MOD", "Optional stiffness modifier (0.0-1.0)", GH_ParamAccess.item, 1.0);

            // Make depth and width optional
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        // Registers all the output parameters for this component.
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Frame Property", "FP", "Frame property definition for the structural model", GH_ParamAccess.item);
        }

    
        // This is the method that actually does the work.
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            string name = string.Empty;
            Material material = null;
            string shape = string.Empty;
            double depth = 0;
            double width = 0;
            double modifier = 1.0;

            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetData(1, ref material)) return;
            if (!DA.GetData(2, ref shape)) return;

            // Optional parameters
            bool hasDepth = DA.GetData(3, ref depth);
            bool hasWidth = DA.GetData(4, ref width);
            DA.GetData(5, ref modifier);

            // Basic validation
            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Frame property name cannot be empty");
                return;
            }

            if (string.IsNullOrWhiteSpace(shape))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Shape cannot be empty");
                return;
            }


            if (material == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid material provided");
                return;
            }

            try
            {
                // Create a new frame property
                FrameProperties frameProperty = new FrameProperties
                {
                    Name = name,
                    MaterialId = material.Id,
                    Shape = shape
                };

                // Set dimensions if provided
                if (hasDepth)
                    frameProperty.Dimensions["depth"] = depth;

                if (hasWidth)
                    frameProperty.Dimensions["width"] = width;

                // Set additional dimensions based on shape type
                SetDefaultDimensions(frameProperty, shape);

                // Set modifier if provided
                if (modifier >= 0 && modifier <= 1.0)
                {
                    frameProperty.Modifiers["axial"] = modifier;
                    frameProperty.Modifiers["shear"] = modifier;
                    frameProperty.Modifiers["flexural"] = modifier;
                    frameProperty.Modifiers["torsional"] = modifier;
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Invalid modifier value ({modifier}). Must be between 0.0 and 1.0. Using default of 1.0.");
                }

                // Output the frame property
                DA.SetData(0, new Utilities.GH_FrameProperties(frameProperty));
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private void SetDefaultDimensions(FrameProperties frameProperty, string shape)
        {
            if (shape.StartsWith("W", StringComparison.OrdinalIgnoreCase) ||
                shape.StartsWith("S", StringComparison.OrdinalIgnoreCase) ||
                shape.StartsWith("HP", StringComparison.OrdinalIgnoreCase))
            {
                // For wide flange shapes, add default web and flange thickness
                if (!frameProperty.Dimensions.ContainsKey("webThickness"))
                    frameProperty.Dimensions["webThickness"] = 0.375; // Default web thickness in inches

                if (!frameProperty.Dimensions.ContainsKey("flangeThickness"))
                    frameProperty.Dimensions["flangeThickness"] = 0.625; // Default flange thickness in inches
            }
            else if (shape.StartsWith("HSS", StringComparison.OrdinalIgnoreCase))
            {
                // For hollow structural sections, add default wall thickness
                if (!frameProperty.Dimensions.ContainsKey("wallThickness"))
                    frameProperty.Dimensions["wallThickness"] = 0.25; // Default wall thickness in inches
            }
            else if (shape.StartsWith("Pipe", StringComparison.OrdinalIgnoreCase))
            {
                // For pipes, adjust dimensions
                if (frameProperty.Dimensions.ContainsKey("width"))
                {
                    frameProperty.Dimensions["outerDiameter"] = frameProperty.Dimensions["width"];
                    frameProperty.Dimensions.Remove("width");
                }

                if (!frameProperty.Dimensions.ContainsKey("wallThickness"))
                    frameProperty.Dimensions["wallThickness"] = 0.25; // Default wall thickness in inches
            }
        }

        // Gets the unique ID for this component. Do not change this ID after release.
        public override Guid ComponentGuid => new Guid("D1E2F3A4-B5C6-D7E8-F9A0-B1C2D3E4F5A6");
    }
}
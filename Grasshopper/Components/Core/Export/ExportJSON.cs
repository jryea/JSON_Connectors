using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Grasshopper.Kernel;
using Core.Converters;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Model;
using Core.Models.Properties;
using Core.Models.Metadata;
using Core.Models.Loads;

namespace Grasshopper.Export
{
    public class ExportJSON : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ExportJSON class.
        /// </summary>
        public ExportJSON()
          : base("Export JSON", "ExpJSON",
              "Export a complete structural model to JSON file",
              "IMEG", "Export")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Structural model to export", GH_ParamAccess.item);
            pManager.AddTextParameter("File Path", "F", "Path to save the JSON file", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Export", "E", "Trigger export (set to true)", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Pretty Print", "P", "Format JSON with indentation for readability", GH_ParamAccess.item, true);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON representation of the model", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if export was successful", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "I", "Result message", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            BaseModel model = null;
            string filePath = string.Empty;
            bool export = false;
            bool prettyPrint = true;

            if (!DA.GetData(0, ref model))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No structural model provided");
                DA.SetData(1, false);
                DA.SetData(2, "No structural model provided");
                return;
            }

            if (!DA.GetData(1, ref filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No file path provided");
                DA.SetData(1, false);
                DA.SetData(2, "No file path provided");
                return;
            }

            DA.GetData(2, ref export);
            DA.GetData(3, ref prettyPrint);

            try
            {
                // Generate JSON string
                string json = JsonConverter.Serialize(model);

                // Set JSON output regardless of export flag
                DA.SetData(0, json);

                // If export is triggered, save to file
                if (export)
                {
                    // Validate file path
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        DA.SetData(1, false);
                        DA.SetData(2, "Invalid file path");
                        return;
                    }

                    // Make sure path ends with .json
                    if (!filePath.ToLower().EndsWith(".json"))
                    {
                        filePath += ".json";
                    }

                    // Create directory if it doesn't exist
                    string directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Save model to file
                    JsonConverter.SaveToFile(model, filePath);

                    // Generate model stats for info message
                    int elementCount = CountElements(model.Elements);
                    int propCount = CountProperties(model.Properties);

                    // Success message
                    DA.SetData(1, true);
                    DA.SetData(2, $"Successfully exported model with {elementCount} elements and {propCount} properties to {filePath}");
                }
                else
                {
                    DA.SetData(1, true);
                    DA.SetData(2, "JSON generated. Set Export to True to save the file.");
                }
            }
            catch (Exception ex)
            {
                DA.SetData(0, string.Empty);
                DA.SetData(1, false);
                DA.SetData(2, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Counts the total number of elements in the container
        /// </summary>
        private int CountElements(ElementContainer elements)
        {
            if (elements == null)
                return 0;

            return elements.Floors.Count +
                   elements.Walls.Count +
                   elements.Beams.Count +
                   elements.Braces.Count +
                   elements.Columns.Count +
                   elements.IsolatedFootings.Count +
                   elements.Joints.Count +
                   elements.ContinuousFootings.Count +
                   elements.Piles.Count +
                   elements.Piers.Count +
                   elements.DrilledPiers.Count;
        }

        /// <summary>
        /// Counts the total number of properties in the container
        /// </summary>
        private int CountProperties(PropertiesContainer properties)
        {
            if (properties == null)
                return 0;

            return properties.Materials.Count +
                   properties.WallProperties.Count +
                   properties.FloorProperties.Count +
                   properties.Diaphragms.Count +
                   properties.FrameProperties.Count;
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
        public override Guid ComponentGuid => new Guid("7f47f0b0-b365-43d5-b83f-32e8e83eaca9");
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Grasshopper.Kernel;
using Core.Converters;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
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
            // Export settings
            pManager.AddTextParameter("File Path", "F", "Path to save the JSON file", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Export", "X", "Trigger export (set to true)", GH_ParamAccess.item, false);

            // Option 2: Build model from containers
            pManager.AddGenericParameter("Metadata", "M", "Project information", GH_ParamAccess.item);
            pManager.AddGenericParameter("Layout", "L", "Container with layout components (grids, levels, etc.)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Properties", "P", "Container with all property definitions", GH_ParamAccess.item);
            pManager.AddGenericParameter("Loads", "LD", "Container with load definitions (optional)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Elements", "E", "Container with all structural elements", GH_ParamAccess.item);


            // Make components optional
            pManager[0].Optional = true;  // File Path is optional if not exporting
            pManager[1].Optional = true;  // Project Info is optional
            pManager[2].Optional = true;  // Layout is optional
            pManager[3].Optional = true;  // Properties is optional
            pManager[4].Optional = true;  // Loads is optional
            pManager[5].Optional = true;  // Elements is optional
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON representation of the model", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if export was successful", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "MSG", "Result message", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            ElementContainer elementContainer = null;
            PropertiesContainer propertiesContainer = null;
            ModelLayoutContainer layoutContainer = null;
            MetadataContainer metadata = null;
            LoadContainer loadContainer = null;
            string filePath = string.Empty;
            bool export = false;

            // Get export settings
            DA.GetData(0, ref filePath);
            DA.GetData(1, ref export);

            // Try to get individual containers
            DA.GetData(2, ref metadata);
            DA.GetData(3, ref layoutContainer);
            DA.GetData(4, ref propertiesContainer);
            DA.GetData(5, ref loadContainer);
            DA.GetData(6, ref elementContainer);


            try
            {
                // Prepare the model - either use the provided one or build a new one
                BaseModel baseModel;
               
                // Build a new model from containers
                baseModel = new BaseModel();

                // Add elements if container is provided
                if (elementContainer != null)
                {
                    baseModel.Elements = elementContainer;
                }

                // Add properties if container is provided
                if (propertiesContainer != null)
                {
                    baseModel.Properties = propertiesContainer;
                }

                // Add model layout components if provided
                if (layoutContainer != null)
                {
                    baseModel.ModelLayout = layoutContainer;
                }

                // Add project info if provided
                if (metadata != null)
                {
                    baseModel.Metadata = metadata;
                }
                else
                {
                    // Create default project info
                    baseModel.Metadata.ProjectInfo.ProjectName = "New Project";
                    baseModel.Metadata.ProjectInfo.CreationDate = DateTime.Now;
                    baseModel.Metadata.ProjectInfo.SchemaVersion = "1.0";
                    baseModel.Metadata.Units.Length = "inches";
                    baseModel.Metadata.Units.Force = "pounds";
                    baseModel.Metadata.Units.Temperature = "fahrenheit";
                }

                // Add loads if provided
                if (loadContainer != null)
                {
                    baseModel.Loads = loadContainer;
                }

                // Generate JSON string
                string json = JsonConverter.Serialize(baseModel);

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
                    JsonConverter.SaveToFile(baseModel, filePath);

                    // Generate model stats for info message
                    int elementCount = CountElements(baseModel.Elements);
                    int propCount = CountProperties(baseModel.Properties);
                    int layoutCount = CountLayoutItems(baseModel.ModelLayout);

                    // Success message
                    DA.SetData(1, true);
                    DA.SetData(2, $"Successfully exported model with {elementCount} elements, {propCount} properties, " +
                              $"and {layoutCount} layout items to {filePath}");
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
        /// Counts the total number of layout items in the container
        /// </summary>
        private int CountLayoutItems(ModelLayoutContainer layout)
        {
            if (layout == null)
                return 0;

            return layout.Grids.Count +
                   layout.Levels.Count +
                   layout.FloorTypes.Count;
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
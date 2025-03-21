using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Model;
using Core.Models.Properties;
using Core.Models.Loads;
using Core.Converters;
using Core.Models.Metadata;

namespace Grasshopper.Export
{
    public class ModelContainerComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ModelContainer class.
        /// </summary>
        public ModelContainerComponent()
          : base("Model Container", "ModCont",
              "Creates a complete structural model for export",
              "IMEG", "Model")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project Name", "PN", "Name of the project", GH_ParamAccess.item, "New Project");
            pManager.AddGenericParameter("Element Container", "EC", "Container with all structural elements", GH_ParamAccess.item);
            pManager.AddGenericParameter("Properties Container", "PC", "Container with all property definitions", GH_ParamAccess.item);
            pManager.AddGenericParameter("Grids", "G", "Grid definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Levels", "L", "Level definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Floor Types", "FT", "Floor type definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Loads", "LD", "Load definitions (optional)", GH_ParamAccess.item);
            pManager.AddTextParameter("Length Unit", "LU", "Length unit (e.g., 'inches', 'feet', 'mm', 'm')", GH_ParamAccess.item, "inches");
            pManager.AddTextParameter("Force Unit", "FU", "Force unit (e.g., 'pounds', 'kips', 'N', 'kN')", GH_ParamAccess.item, "pounds");
            pManager.AddTextParameter("Temperature Unit", "TU", "Temperature unit (e.g., 'fahrenheit', 'celsius')", GH_ParamAccess.item, "fahrenheit");
            pManager.AddTextParameter("Export Path", "EP", "Path to export the model (optional)", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Export", "E", "Export the model to file", GH_ParamAccess.item, false);

            // Make some parameters optional
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[10].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Complete structural model", GH_ParamAccess.item);
            pManager.AddTextParameter("JSON", "J", "JSON string representation of the model", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if model was successfully created/exported", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Information about the model/export operation", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            string projectName = "New Project";
            ElementContainer elementContainer = null;
            PropertiesContainer propertiesContainer = null;
            List<Grid> grids = new List<Grid>();
            List<Level> levels = new List<Level>();
            List<FloorType> floorTypes = new List<FloorType>();
            LoadContainer loadContainer = null;
            string lengthUnit = "inches";
            string forceUnit = "pounds";
            string temperatureUnit = "fahrenheit";
            string exportPath = string.Empty;
            bool exportModel = false;

            if (!DA.GetData(0, ref projectName)) return;
            if (!DA.GetData(1, ref elementContainer)) return;
            DA.GetData(2, ref propertiesContainer);
            DA.GetDataList(3, grids);
            DA.GetDataList(4, levels);
            DA.GetDataList(5, floorTypes);
            DA.GetData(6, ref loadContainer);
            DA.GetData(7, ref lengthUnit);
            DA.GetData(8, ref forceUnit);
            DA.GetData(9, ref temperatureUnit);
            DA.GetData(10, ref exportPath);
            DA.GetData(11, ref exportModel);

            try
            {
                // Create base model
                BaseModel model = new BaseModel();

                // Set metadata
                model.ProjectInfo.ProjectName = projectName;
                model.ProjectInfo.CreationDate = DateTime.Now;
                model.ProjectInfo.SchemaVersion = "1.0";

                // Set units
                model.Units.Length = lengthUnit;
                model.Units.Force = forceUnit;
                model.Units.Temperature = temperatureUnit;

                // Add elements if container is provided
                if (elementContainer != null)
                {
                    model.Elements = elementContainer;
                }

                // Add properties if container is provided
                if (propertiesContainer != null)
                {
                    model.Properties = propertiesContainer;
                }

                // Add structural model components
                model.Model.Grids = grids;
                model.Model.Levels = levels;
                model.Model.FloorTypes = floorTypes;

                // Add loads if provided
                if (loadContainer != null)
                {
                    model.Loads = loadContainer;
                }

                // Generate JSON representation
                string json = JsonConverter.Serialize(model);

                // Export model if requested
                bool exportSuccess = false;
                string info = "Model created successfully.";

                if (exportModel && !string.IsNullOrEmpty(exportPath))
                {
                    try
                    {
                        JsonConverter.SaveToFile(model, exportPath);
                        exportSuccess = true;
                        info = $"Model exported successfully to {exportPath}";
                    }
                    catch (Exception ex)
                    {
                        exportSuccess = false;
                        info = $"Failed to export model: {ex.Message}";
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, info);
                    }
                }
                else if (exportModel && string.IsNullOrEmpty(exportPath))
                {
                    info = "Export path not provided. Model created but not exported.";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, info);
                }

                // Generate model summary
                string modelSummary = GenerateModelSummary(model);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, modelSummary);

                // Set outputs
                DA.SetData(0, model);
                DA.SetData(1, json);
                DA.SetData(2, true);
                DA.SetData(3, info);
            }
            catch (Exception ex)
            {
                DA.SetData(2, false);
                DA.SetData(3, $"Error creating model: {ex.Message}");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// Generates a summary of the model contents
        /// </summary>
        private string GenerateModelSummary(BaseModel model)
        {
            int elementCount = model.Elements.Floors.Count +
                              model.Elements.Walls.Count +
                              model.Elements.Beams.Count +
                              model.Elements.Braces.Count +
                              model.Elements.Columns.Count +
                              model.Elements.IsolatedFootings.Count +
                              model.Elements.Joints.Count +
                              model.Elements.ContinuousFootings.Count +
                              model.Elements.Piles.Count +
                              model.Elements.Piers.Count +
                              model.Elements.DrilledPiers.Count;

            int propertyCount = (model.Properties != null) ?
                              model.Properties.Materials.Count +
                              model.Properties.WallProperties.Count +
                              model.Properties.FloorProperties.Count +
                              model.Properties.Diaphragms.Count +
                              model.Properties.PiersSpandrels.Count +
                              model.Properties.FrameProperties.Count : 0;

            int modelComponentCount = model.Model.Grids.Count +
                                     model.Model.Levels.Count +
                                     model.Model.FloorTypes.Count;

            return $"Model Summary: " +
                   $"Project: {model.ProjectInfo.ProjectName}, " +
                   $"Elements: {elementCount}, " +
                   $"Properties: {propertyCount}, " +
                   $"Model Components: {modelComponentCount}, " +
                   $"Units: {model.Units.Length}/{model.Units.Force}/{model.Units.Temperature}";
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
        public override Guid ComponentGuid => new Guid("7F8E9D0C-1B2A-3C4D-5E6F-7A8B9C0D1E2F");
    }
}
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.IO;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Metadata;
using Core.Models.Loads;
using Core.Converters;
using Grasshopper.Utilities;

namespace JSON_Connectors.Components.Core.Export
{
    public class ExportJSONComponent : GH_Component
    {
        public ExportJSONComponent()
          : base("Export JSON", "ExpJSON",
              "Export a complete structural model to JSON file",
              "IMEG", "Export")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "Path to save the JSON file", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Export", "X", "Trigger export (set to true)", GH_ParamAccess.item, false);
            pManager.AddGenericParameter("Metadata", "M", "Metadata container", GH_ParamAccess.item);
            pManager.AddGenericParameter("Layout", "L", "Model layout container", GH_ParamAccess.item);
            pManager.AddGenericParameter("Properties", "P", "Properties container", GH_ParamAccess.item);
            pManager.AddGenericParameter("Loads", "LD", "Loads container (optional)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Elements", "E", "Elements container", GH_ParamAccess.item);

            for (int i = 2; i < 7; i++)
                pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON representation of the model", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if export was successful", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "MSG", "Result message", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filePath = string.Empty;
            bool export = false;
            object metadataObj = null;
            object layoutObj = null;
            object propertiesObj = null;
            object elementsObj = null;
            object loadsObj = null;

            DA.GetData(0, ref filePath);
            DA.GetData(1, ref export);
            DA.GetData(2, ref metadataObj);
            DA.GetData(3, ref layoutObj);
            DA.GetData(4, ref propertiesObj);
            DA.GetData(5, ref loadsObj);
            DA.GetData(6, ref elementsObj);

            try
            {
                // Create a new model
                BaseModel model = new BaseModel();

                // Extract metadata
                MetadataContainer metadata = ExtractContainer<MetadataContainer>(metadataObj);
                if (metadata != null)
                    model.Metadata = metadata;

                // Extract model layout
                ModelLayoutContainer layout = ExtractContainer<ModelLayoutContainer>(layoutObj);
                if (layout != null)
                    model.ModelLayout = layout;

                // Extract properties
                PropertiesContainer properties = ExtractContainer<PropertiesContainer>(propertiesObj);
                if (properties != null)
                    model.Properties = properties;

                // Extract elements
                ElementContainer elements = ExtractContainer<ElementContainer>(elementsObj);
                if (elements != null)
                    model.Elements = elements;

                // Extract loads
                LoadContainer loads = ExtractContainer<LoadContainer>(loadsObj);
                if (loads != null)
                    model.Loads = loads;

                // Always ensure default metadata
                if (model.Metadata.ProjectInfo == null)
                {
                    model.Metadata.ProjectInfo = new ProjectInfo
                    {
                        ProjectName = "New Project",
                        CreationDate = DateTime.Now,
                        SchemaVersion = "1.0"
                    };
                }

                if (model.Metadata.Units == null || model.Metadata.Units.Length == null)
                {
                    model.Metadata.Units = new Units
                    {
                        Length = "inches",
                        Force = "pounds",
                        Temperature = "fahrenheit"
                    };
                }

                // Generate JSON string
                string json = JsonConverter.Serialize(model);
                DA.SetData(0, json);

                // Export to file if requested
                if (export)
                {
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        DA.SetData(1, false);
                        DA.SetData(2, "Invalid file path");
                        return;
                    }

                    // Ensure .json extension
                    if (!filePath.ToLower().EndsWith(".json"))
                        filePath += ".json";

                    // Create directory if needed
                    string directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    // Save to file
                    JsonConverter.SaveToFile(model, filePath);

                    DA.SetData(1, true);
                    DA.SetData(2, $"Successfully exported model to {filePath}");
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

        private T ExtractContainer<T>(object obj) where T : class, new()
        {
            if (obj == null)
                return new T(); // Return a new instance instead of null

            // Check if it's already our model type
            if (obj is T model)
                return model;

            // Check if it's our Goo wrapper
            if (obj is GH_ModelGoo<T> ghModel)
                return ghModel.Value;

            // Check if it's a general Grasshopper type that can be cast
            if (obj is Grasshopper.Kernel.Types.IGH_Goo goo)
            {
                T result = null;
                if (goo.CastTo<T>(out result))
                    return result;
            }

            // Try to interpret specific container types based on contents
            if (typeof(T) == typeof(MetadataContainer))
            {
                // Handle case where we have ProjectInfo and/or Units but not a container
                MetadataContainer container = new MetadataContainer();

                if (obj is ProjectInfo projectInfo)
                {
                    container.ProjectInfo = projectInfo;
                    return container as T;
                }
                else if (obj is Units units)
                {
                    container.Units = units;
                    return container as T;
                }
            }

            // If we got here, log the type we received for debugging
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                $"Could not extract {typeof(T).Name} from input of type {obj?.GetType().Name ?? "null"}");

            // Return a new instance with defaults rather than null
            return new T();
        }

        public override Guid ComponentGuid => new Guid("7f47f0b0-b365-43d5-b83f-32e8e83eaca9");
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Core.Models.Properties;

namespace Grasshopper.Export
{
    public class PropertiesContainerComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PropertiesContainer class.
        /// </summary>
        public PropertiesContainerComponent()
          : base("Properties Container", "PropsCont",
              "Collects all property definitions into a single container for the structural model",
              "IMEG", "Properties")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Materials", "M", "Material definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Wall Properties", "WP", "Wall property definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Floor Properties", "FP", "Floor property definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Diaphragms", "D", "Diaphragm definitions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Pier Spandrels", "PS", "Pier/spandrel configurations", GH_ParamAccess.list);
            pManager.AddGenericParameter("Frame Properties", "FRP", "Frame property definitions", GH_ParamAccess.list);

            // Make all parameters optional
            for (int i = 0; i < 6; i++)
            {
                pManager[i].Optional = true;
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Properties Container", "PC", "Container with all property definitions for the structural model", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input data
            List<Material> materials = new List<Material>();
            List<WallProperties> wallProperties = new List<WallProperties>();
            List<FloorProperties> floorProperties = new List<FloorProperties>();
            List<Diaphragm> diaphragms = new List<Diaphragm>();
            List<FrameProperties> frameProperties = new List<FrameProperties>();

            // Get data - all inputs are optional
            DA.GetDataList(0, materials);
            DA.GetDataList(1, wallProperties);
            DA.GetDataList(2, floorProperties);
            DA.GetDataList(3, diaphragms);
            DA.GetDataList(4, frameProperties);

            try
            {
                // Create properties container
                PropertiesContainer propertiesContainer = new PropertiesContainer();

                // Add all properties
                propertiesContainer.Materials.AddRange(materials);
                propertiesContainer.WallProperties.AddRange(wallProperties);
                propertiesContainer.FloorProperties.AddRange(floorProperties);
                propertiesContainer.Diaphragms.AddRange(diaphragms);
                propertiesContainer.FrameProperties.AddRange(frameProperties);

                // Output container count info
                string countInfo = $"Created properties container with: " +
                                  $"{materials.Count} materials, " +
                                  $"{wallProperties.Count} wall properties, " +
                                  $"{floorProperties.Count} floor properties, " +
                                  $"{diaphragms.Count} diaphragms, " +
                                  $"{frameProperties.Count} frame properties";

                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, countInfo);

                // Set output
                DA.SetData(0, propertiesContainer);
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
        public override Guid ComponentGuid => new Guid("D3C2B1A0-9F8E-7D6C-5B4A-3E2F1D0C9B8A");
    }
}
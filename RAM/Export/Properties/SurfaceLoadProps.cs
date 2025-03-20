using System;
using System.Collections.Generic;

using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using RAMDATAACCESSLib;
using Grasshopper_RAM.Utilities;

namespace JSON_Connectors.Connectors.RAM.Export
{
    public class SurfaceLoadProperties : GH_Component
    {
        public SurfaceLoadProperties()
            : base("SurfaceLoad Properties", "RMS", "Create RAM surface load properties", "SPEED", "RAM")
        {
        }

        public override Guid ComponentGuid => new Guid("7ae3b1e3-d5a5-4b95-a25a-a035de9c94c9");
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("Surface load name", "SN", "RAM surface load name", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Property Id", "ID", "RAM slab property Id", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;
            List<string> surfaceLoadName = new List<string>();

            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetDataList(1, surfaceLoadName)) return;

            // Surface Load Properties
            double constDeadLoad = 0.0;
            double constLiveLoad = 0.0;
            double deadLoad = 0.0;
            double liveLoad = 0.0;
            double massDeadLoad = 0.0;
            double partitionLoad = 0.0;
            ELoadCaseType liveLoadType = ELoadCaseType.LiveReducibleLCa;

            // Open Model and Database
            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;
            List<int> surfaceLoadIds = new List<int>();
            ISurfaceLoadPropertySets surfaceLoadProps = model.GetSurfaceLoadPropertySets();

            for (int i = 0; i < surfaceLoadName.Count; i++)
            {
                try
                {
                    ISurfaceLoadPropertySet surfaceLoadProp = surfaceLoadProps.Add(surfaceLoadName[i]);
                    surfaceLoadIds.Add(surfaceLoadProp.lUID);
                }
                catch (Exception e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                }
            }

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetDataList(0, surfaceLoadIds);
        }
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.IMEG_Logo_Grasshopper;
            }
        }
    }
}

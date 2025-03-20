using System;
using System.Collections.Generic;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

using RAMDATAACCESSLib;

namespace JSON_Connectors.Connectors.RAM.Export
{
    public class FloorType : GH_Component
    {
        public FloorType()
            : base("Floor Type", "FTYPE", "Create RAM Floor Type", "SPEED", "RAM")
        {
        }

        public override Guid ComponentGuid => new Guid("8b384189-15d3-40c3-8f9b-235b61d956fc");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FN", "RAM File name", GH_ParamAccess.item);
            pManager.AddTextParameter("Floor Type Name", "FTN", "RAM floor type name", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "File", "RAM Structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("Floor Type Name", "Floor Type", "RAM floor type name", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;
            List<string> floorTypeNames = new List<string>();
            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetDataList(1, floorTypeNames)) return;

            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;
            IFloorTypes floorTypes = model.GetFloorTypes();

            IFloorType floorType;
            foreach (string floorTypeName in floorTypeNames)
            {
                floorType = floorTypes.Add(floorTypeName);
            }

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetData(0, fileName);
            DA.SetDataList(1, floorTypeNames);
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

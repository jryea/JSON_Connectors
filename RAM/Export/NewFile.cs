using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

using RAMDATAACCESSLib;

namespace JSON_Connectors.Connectors.RAM.Export
{
    public class NewFile : GH_Component
    {
        public NewFile()
            : base("New File", "RMNF", "Create New RAM structural system file", "SPEED", "RAM")
        {
        }

        public override Guid ComponentGuid => new Guid("3b00ed8b-2796-4000-8233-66d0eb0119a8");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM Structural model file name", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "File", "RAM Structural model file name", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;

            if (!DA.GetData(0, ref fileName)) return;

            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            EUnits units = EUnits.eUnitsEnglish;
            db.CreateNewDatabase2(fileName, units, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetData(0, fileName);
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

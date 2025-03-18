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
    public class SlabProps : GH_Component
    {
        public SlabProps()
            : base("Slab Properties", "RMS", "Create RAM slab properties", "SPEED", "RAM")
        {
        }

        public override Guid ComponentGuid => new Guid("0982a1e3-b05e-41d7-94b4-eec81581032a");
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("Slab Name", "SN", "RAM slab name", GH_ParamAccess.list);
            pManager.AddTextParameter("Thickness", "T", "Slab thickness", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Property Id", "ID", "RAM slab property Id", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;
            List<string> slabName = new List<string>();
            List<double> thickness = new List<double>();

            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetDataList(1, slabName)) return;
            if (!DA.GetDataList(2, thickness)) return;

            // NonComposite Deck Properties    
            bool useElasticModulus = false;
            double bendingCrackedFfactor = 1.0;
            double diaphragmCrackedFfactor = 0.0;
            double elasticModulus = 0.0;
            double fpc = 0.0;
            double poissonsRation = 0.0;
            double selfWeight = 0.0;
            double unitWeight = 145;


            // Open Model and Database
            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;
            List<int> slabPropertyIds = new List<int>();
            IConcSlabProps slabProps = model.GetConcreteSlabProps();

            for (int i = 0; i < slabName.Count; i++)
            {
                try
                {
                    IConcSlabProp slabProp = slabProps.Add(slabName[i], thickness[i], selfWeight);
                    slabPropertyIds.Add(slabProp.lUID);
                }
                catch (Exception e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                }
            }

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetDataList(0, slabPropertyIds);
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

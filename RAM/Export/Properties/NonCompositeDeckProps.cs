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
    public class NonCompositeDeckProps : GH_Component
    {
        public NonCompositeDeckProps()
            : base("NonComposite Properties", "RMCD", "Create RAM composite deck properties", "SPEED", "RAM")
        {
        }

        public override Guid ComponentGuid => new Guid("403aceda-5a8f-42a8-a4a9-de1a2460990d");
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("NonCompositeDeck Name", "CDN", "RAM noncomposite deck name", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Property Id", "ID", "RAM deck property Id", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;
            List<string> deckName = new List<string>();

            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetDataList(1, deckName)) return;

            // NonComposite Deck Properties    
            double effectiveThickness = 0.0;
            double elasticModulus = 0.0;
            double poissonsRation = 0.0;
            double selfWeight = 0.0;

            // Open Model and Database
            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;
            List<int> deckPropertyIds = new List<int>();
            INonCompDeckProps nonCompDeckProps = model.GetNonCompDeckProps();

            for (int i = 0; i < deckName.Count; i++)
            {
                try
                {
                    INonCompDeckProp nonCompDeckProp = nonCompDeckProps.Add(deckName[i]);
                    deckPropertyIds.Add(nonCompDeckProp.lUID);
                }
                catch (Exception e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                }
            }

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetDataList(0, deckPropertyIds);
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

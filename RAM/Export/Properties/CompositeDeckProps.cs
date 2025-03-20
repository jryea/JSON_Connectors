using System;
using System.Collections.Generic;

using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using RAMDATAACCESSLib;
using Grasshopper_RAM.Utilities;
using Grasshopper_RAM.Properties;
using System.Runtime.Versioning;

namespace JSON_Connectors.Connectors.RAM.Export
{
    public class CompositeDeckProps : GH_Component
    {
        public CompositeDeckProps()
            : base("Composite Properties", "RMCD", "Create RAM composite deck properties", "SPEED", "RAM")
        {
        }

        public override Guid ComponentGuid => new Guid("cad5943f-2f5d-4437-86ac-8a44ee690fcc");
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("CompositeDeck Name", "CDN", "RAM composite deck name", GH_ParamAccess.list);
            pManager.AddTextParameter("Deck Type", "DT", "RAM composite structural deck type", GH_ParamAccess.list);
            pManager.AddNumberParameter("Topping Thickness", "TH", "Topping thickness", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Deck Gage", "GA", "RAM Composite Deck Gage", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Property Id", "ID", "RAM deck property Id", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;
            List<string> deckName = new List<string>();
            List<string> deckType = new List<string>();
            List<double> toppingThickness = new List<double>();
            List<int> deckGage = new List<int>();

            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetDataList(1, deckName)) return;
            if (!DA.GetDataList(2, deckType)) return;
            if (!DA.GetDataList(3, toppingThickness)) return;
            if (!DA.GetDataList(4, deckGage)) return;

            // Composite Deck Properties    
            double selfWeight;
            double studLength = 4.0;
            //bool isShored = false;
            //double effectiveThickness = 0.0;    
            //double elasticModulus = 0.0;
            //double fpc = 0.0;  
            //double poissonsRation = 0.0;
            //double studDiameter = 0.0;  
            //double studFu = 65;   
            //double unitWeight = 145;

            // Open Model and Database
            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;

            List<int> deckPropertyIds = new List<int>();
            ICompDeckProps compDeckProps = model.GetCompositeDeckProps();

            for (int i = 0; i < deckName.Count; i++)
            {
                try
                {
                    GetDeckProperties(deckType[i], deckGage[i], out selfWeight);
                    ICompDeckProp compDeckProp = compDeckProps.Add2(deckName[i], deckType[i], toppingThickness[i], studLength);
                    compDeckProp.dSelfWtDeck = selfWeight;

                    deckPropertyIds.Add(compDeckProp.lUID);
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

        private void GetDeckProperties(string deckType, int deckGage, out double selfWeight)
        {
            if (deckType == "VULCRAFT 1.5VL")
            {
                if (deckGage == 22)
                {
                    selfWeight = 1.6;
                }
                else if (deckGage == 20)
                {
                    selfWeight = 2.0;
                }
                else if (deckGage == 19)
                {
                    selfWeight = 2.3;
                }
                else if (deckGage == 18)
                {
                    selfWeight = 2.6;
                }
                else if (deckGage == 16)
                {
                    selfWeight = 3.3;
                }
                else
                {
                    throw new Exception("Deck Gage not supported");
                }
            }
            else if (deckType == "VULCRAFT 2VL")
            {
                if (deckGage == 22)
                {
                    selfWeight = 1.6;
                }
                else if (deckGage == 20)
                {
                    selfWeight = 1.9;
                }
                else if (deckGage == 19)
                {
                    selfWeight = 2.2;
                }
                else if (deckGage == 18)
                {
                    selfWeight = 2.5;
                }
                else if (deckGage == 16)
                {
                    selfWeight = 3.2;
                }
                else
                {
                    throw new Exception("Deck Gage not supported");
                }

            }
            else if (deckType == "VULCRAFT 3VL")
            {
                if (deckGage == 22)
                {
                    selfWeight = 1.7;
                }
                else if (deckGage == 20)
                {
                    selfWeight = 2.1;
                }
                else if (deckGage == 19)
                {
                    selfWeight = 2.4;
                }
                else if (deckGage == 18)
                {
                    selfWeight = 2.7;
                }
                else if (deckGage == 16)
                {
                    selfWeight = 3.5;
                }
                else
                {
                    throw new Exception("Deck Gage not supported");
                }
            }
            else
            {
                throw new Exception("Deck Type not supported");
            }
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

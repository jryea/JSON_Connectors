using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

using RAMDATAACCESSLib;
using Grasshopper_RAM.Utilities;

namespace JSON_Connectors.Connectors.RAM.Export
{
    public class LayoutBeam : GH_Component
    {
        public LayoutBeam()
            : base("Layout Beam", "RMLC", "Create RAM layout beams", "SPEED", "RAM")
        {
        }

        public override Guid ComponentGuid => new Guid("73a622f2-eae7-4910-81bb-1b61280b10f7");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("Floor Type", "FT", "RAM floor type", GH_ParamAccess.list);
            pManager.AddLineParameter("Beam Line", "BL", "RAM beam line", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Beam Material", "MAT", "RAM beam material", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Joist", "J", "RAM bar joist", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Beam ID", "BID", "RAM beam ID", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;
            List<string> floorTypeNames = new List<string>();
            GH_Structure<GH_Line> beamLines = new GH_Structure<GH_Line>();
            int beamMaterial = 0;
            List<bool> isJoist = new List<bool>();

            List<int> beamIds = new List<int>();

            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetDataList(1, floorTypeNames)) return;
            if (!DA.GetDataTree(2, out beamLines)) return;
            if (!DA.GetData(3, ref beamMaterial)) return;
            if (!DA.GetDataList(4, isJoist)) return;

            // Open Model and Database
            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;

            // Get Column Material
            EMATERIALTYPES beamMaterialType = Helpers.GetMaterialType(beamMaterial);

            List<IFloorType> floorTypes = Helpers.GetFloorTypes(model, floorTypeNames);

            beamIds = CreateBeams(model, floorTypes, beamLines, beamMaterialType, isJoist);

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetDataList(0, beamIds);
        }

        private List<int> CreateBeams(IModel model, List<IFloorType> floorTypes,
            GH_Structure<GH_Line> beamLines, EMATERIALTYPES beamMaterialType, List<bool> isJoist)
        {
            List<int> beamIds = new List<int>();
            List<List<Line>> beamLinesNestedList = Helpers.ConvertLinesToNestedList(beamLines);


            for (int i = 0; i < floorTypes.Count; i++)
            {
                // If joist=true, replace material with joist material 
                EMATERIALTYPES beamMaterial;
                if (isJoist[i])
                {
                    beamMaterial = EMATERIALTYPES.ESteelJoistMat;
                }
                else
                {
                    beamMaterial = beamMaterialType;
                }

                IFloorType floorType = floorTypes[i];
                List<Line> beamLinesList = beamLinesNestedList[i];

                for (int j = 0; j < beamLinesList.Count; j++)
                {
                    List<double> lineCoordinates = GetLineCoordinates(beamLinesList[j]);

                    double beamX1 = lineCoordinates[0];
                    double beamY1 = lineCoordinates[1];
                    double beamZ1 = lineCoordinates[2];
                    double beamX2 = lineCoordinates[3];
                    double beamY2 = lineCoordinates[4];
                    double beamZ2 = lineCoordinates[5];

                    ILayoutBeam beam = floorType.GetLayoutBeams().Add(beamMaterial, beamX1, beamY1, beamZ1, beamX2, beamY2, beamZ2);
                    beamIds.Add(beam.lUID);
                }
            }
            return beamIds;
        }

        private List<double> GetLineCoordinates(Line beamLine)
        {
            double beamX1 = beamLine.FromX * 12;
            double beamY1 = beamLine.FromY * 12;
            double beamZ1 = 0;
            double beamX2 = beamLine.ToX * 12;
            double beamY2 = beamLine.ToY * 12;
            double beamZ2 = 0;

            return new List<double> { beamX1, beamY1, beamZ1, beamX2, beamY2, beamZ2 };
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

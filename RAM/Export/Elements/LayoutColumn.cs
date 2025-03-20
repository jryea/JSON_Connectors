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
    public class LayoutColumn : GH_Component
    {
        public LayoutColumn()
            : base("Layout Column", "RMCC", "Create Layout Columns in RAM", "SPEED", "RAM")
        {
        }
        public override Guid ComponentGuid => new Guid("b231284e-890f-45f9-a328-7ccf4a96acc7");
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("Floor Type", "FTN", "RAM floor type", GH_ParamAccess.list);
            pManager.AddLineParameter("Column Line", "CL", "RAM column line", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Column Material", "CM", "RAM column material", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Column ID", "CID", "RAM column ID", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;
            List<string> floorTypeNames = new List<string>();
            GH_Structure<GH_Line> columnLines = new GH_Structure<GH_Line>();
            int columnMaterial = 0;

            List<int> columnIds = new List<int>();

            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetDataList(1, floorTypeNames)) return;
            if (!DA.GetDataTree(2, out columnLines)) return;
            if (!DA.GetData(3, ref columnMaterial)) return;

            // Open Model and Database
            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;

            // Get Column Material
            EMATERIALTYPES columnMaterialType = Helpers.GetMaterialType(columnMaterial);

            List<IFloorType> floorTypes = Helpers.GetFloorTypes(model, floorTypeNames);

            columnIds = CreateColumns(model, floorTypes, columnLines, columnMaterialType);

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetDataList(0, columnIds);
        }

        private List<int> CreateColumns(IModel model, List<IFloorType> floorTypes, GH_Structure<GH_Line> columnLines, EMATERIALTYPES columnMaterialType)
        {
            List<int> columnIds = new List<int>();
            List<List<Line>> columnLinesNestedList = Helpers.ConvertLinesToNestedList(columnLines);

            for (int i = 0; i < columnLinesNestedList.Count; i++)
            {
                IFloorType floorType = floorTypes[i];
                List<Line> columnLinesList = columnLinesNestedList[i];
                for (int j = 0; j < columnLinesList.Count; j++)
                {
                    Line columnLine = columnLinesList[j];
                    List<double> lineCoordinates = getLineCoordinates(columnLine);
                    double colX = lineCoordinates[0];
                    double colY = lineCoordinates[1];
                    double colBaseZ = lineCoordinates[2];
                    double colTopZ = lineCoordinates[3];
                    ILayoutColumn column = floorType.GetLayoutColumns().Add(columnMaterialType, colX, colY, colTopZ, colBaseZ);
                    columnIds.Add(column.lUID);
                }
            }
            return columnIds;
        }

        private List<double> getLineCoordinates(Line columnLine)
        {
            double colX = columnLine.FromX * 12;
            double colY = columnLine.FromY * 12;
            double colBaseZ = 0;
            double colTopZ = 0;
            return new List<double> { colX, colY, colBaseZ, colTopZ };
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

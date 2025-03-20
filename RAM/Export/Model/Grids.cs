using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

using RAMDATAACCESSLib;
using Eto.Forms;

namespace JSON_Connectors.Connectors.RAM.Export
{
    public class Grids : GH_Component
    {
        public Grids()
            : base("Grid", "RMCG", "Create Grids in RAM", "SPEED", "RAM")
        {
        }

        public override Guid ComponentGuid => new Guid("2c68ed07-753f-4641-bf4b-1a5216c6172a");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM Structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("Floor Type", "FT", "RAM Floor Type", GH_ParamAccess.item);
            pManager.AddTextParameter("X Grid Label", "GXLBL", "RAM X grid label", GH_ParamAccess.list);
            pManager.AddLineParameter("X Grid Line", "GXL", "RAM X grid line", GH_ParamAccess.list);
            pManager.AddTextParameter("Y Grid Label", "GYLBL", "RAM Y grid label", GH_ParamAccess.list);
            pManager.AddLineParameter("Y Grid Line", "GYL", "RAM Y grid line", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Grid ID", "GID", "RAM grid ID", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;
            string floorTypeName = string.Empty;
            List<string> xGridLabels = new List<string>();
            List<Line> xGridLines = new List<Line>();
            List<string> yGridLabels = new List<string>();
            List<Line> yGridLines = new List<Line>();

            List<int> gridIds = new List<int>();

            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetData(1, ref floorTypeName)) return;
            if (!DA.GetDataList(2, xGridLabels)) return;
            if (!DA.GetDataList(3, xGridLines)) return;
            if (!DA.GetDataList(4, yGridLabels)) return;
            if (!DA.GetDataList(5, yGridLines)) return;

            if (xGridLabels.Count != xGridLines.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "X Grid Labels and Lines count mismatch");
                return;
            }
            if (yGridLabels.Count != yGridLines.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Y Grid Labels and Lines count mismatch");
                return;
            }

            // Open Model and Database
            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;

            // Get Existing Grids
            IGridSystems gridSystems = model.GetGridSystems();
            IGridSystem gridSystem;

            if (gridSystems.GetCount() > 0)
            {
                gridSystem = gridSystems.GetAt(0);
            }

            else
            {
                gridSystem = gridSystems.Add("SpeedGrids");
            }

            IModelGrids grids = gridSystem.GetGrids();

            // Add Grids
            List<int> xGridIds = AddGrids(grids, xGridLabels, xGridLines, "X");
            List<int> yGridIds = AddGrids(grids, yGridLabels, yGridLines, "Y");

            gridIds.AddRange(xGridIds);
            gridIds.AddRange(yGridIds);

            // Add Grid System to Floor
            IFloorTypes floorTypes = model.GetFloorTypes();
            IFloorType floorType;

            for (int i = 0; i < floorTypes.GetCount(); i++)
            {
                IFloorType curFloorType = floorTypes.GetAt(i);

                if (curFloorType.strLabel == floorTypeName)
                {
                    DAArray gridSystemArray = curFloorType.GetGridSystemIDArray();
                    int gridArray = 0;
                    gridSystemArray.Add(gridSystem.lUID, ref gridArray);
                    //gridSystemArray.Add(gridSystem);
                    curFloorType.SetGridSystemIDArray(gridSystemArray);
                }
            }

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetDataList(0, gridIds);
        }

        private List<int> AddGrids(IModelGrids grids, List<string> gridLabels, List<Line> gridLines, string direction)
        {
            List<int> gridIds = new List<int>();
            EGridAxis gridAxis;
            double gridCoord;

            if (direction == "X")
            {
                gridAxis = EGridAxis.eGridXorRadialAxis;
            }
            else
            {
                gridAxis = EGridAxis.eGridYorCircularAxis;
            }

            for (int i = 0; i < gridLines.Count; i++)
            {
                if (direction == "X")
                {
                    gridCoord = gridLines[i].FromX;
                }
                else
                {
                    gridCoord = gridLines[i].FromY;
                }

                IModelGrid grid = grids.Add(gridLabels[i], gridAxis, gridCoord * 12);
                gridIds.Add(grid.lUID);
            }

            return gridIds;
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

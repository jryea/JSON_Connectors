using System;
using System.Collections.Generic;

using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using RAMDATAACCESSLib;
using Grasshopper_RAM.Utilities;
using System.Linq;
using System.Drawing.Text;

namespace JSON_Connectors.Connectors.RAM.Export
{
    public class WallExporter : GH_Component
    {
        public WallExporter()
            : base("Layout Wall", "RMW", "Create RAM layout wall", "SPEED", "RAM")
        {
        }

        public override Guid ComponentGuid => new Guid("55671f96-e75a-4d5e-aadc-a7c0947b4ddf");
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("Floor Type", "FTN", "RAM floor type", GH_ParamAccess.list);
            pManager.AddSurfaceParameter("Wall Surface", "WS", "Wall surface", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Thickness", "T", "Wall thickness", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Wall Id", "ID", "RAM wall Id", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string fileName = string.Empty;
            List<string> floorTypeNames = new List<string>();
            GH_Structure<GH_Surface> wallSurfaces = new GH_Structure<GH_Surface>();
            GH_Structure<GH_Number> wallThickness = new GH_Structure<GH_Number>();

            List<int> wallIds = new List<int>();

            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetDataList(1, floorTypeNames)) return;
            if (!DA.GetDataTree(2, out wallSurfaces)) return;
            if (!DA.GetDataTree(3, out wallThickness)) return;

            // Open Model and Database
            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;

            // Get FloorTypes
            List<IFloorType> floorTypes = Helpers.GetFloorTypes(model, floorTypeNames);
            List<List<Brep>> surfaceLists = GetSurfaceLists(wallSurfaces);
            List<List<double>> wallThicknessLists = GetWallThicknessLists(wallThickness);

            wallIds = CreateWalls(model, floorTypes, surfaceLists, wallThicknessLists);

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetDataList(0, wallIds);
        }

        private List<List<double>> GetWallThicknessLists(GH_Structure<GH_Number> wallThicknessTree)
        {
            List<List<double>> wallThicknessLists = new List<List<double>>();
            for (int i = 0; i < wallThicknessTree.PathCount; i++)
            {
                GH_Path path = wallThicknessTree.get_Path(i);
                List<GH_Number> wallThicknessBranch = wallThicknessTree.get_Branch(path) as List<GH_Number>;
                List<double> wallThicknessList = wallThicknessBranch.Select(wallThickness => wallThickness.Value).ToList();
                wallThicknessLists.Add(wallThicknessList);
            }
            return wallThicknessLists;
        }

        private List<int> CreateWalls(IModel model, List<IFloorType> floorTypes, List<List<Brep>> wallSurfaces, List<List<double>> wallThickness)
        {
            List<int> wallIdsList = new List<int>();
            for (int i = 0; i < floorTypes.Count; i++)
            {
                ILayoutWalls walls = floorTypes[i].GetLayoutWalls();

                for (int j = 0; j < wallSurfaces[i].Count; j++)
                {
                    Brep wallSurface = wallSurfaces[i][j];
                    double thickness = wallThickness[i][j];
                    Curve wallCurve = wallSurface.Edges[1].ToNurbsCurve();

                    double startX = wallCurve.PointAtStart.X * 12;
                    double startY = wallCurve.PointAtStart.Y * 12;
                    double endX = wallCurve.PointAtEnd.X * 12;
                    double endY = wallCurve.PointAtEnd.Y * 12;

                    ILayoutWall wall = walls.Add(EMATERIALTYPES.EConcreteMat, startX, startY, 0.0, 0.0, endX, endY, 0.0, 0.0, thickness);
                    wallIdsList.Add(wall.lUID);
                }
            }
            return wallIdsList;
        }

        private static List<List<Brep>> GetSurfaceLists(GH_Structure<GH_Surface> surfaces)
        {
            List<List<Brep>> surfaceLists = new List<List<Brep>>();
            for (int i = 0; i < surfaces.PathCount; i++)
            {
                GH_Path path = surfaces.get_Path(i);
                List<GH_Surface> surfaceBranch = surfaces.get_Branch(path) as List<GH_Surface>;

                List<Brep> surfaceList = surfaceBranch.Select(Surface => Surface.Value).ToList();

                surfaceLists.Add(surfaceList);
            }
            return surfaceLists;
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

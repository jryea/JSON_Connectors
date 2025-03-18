using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using RAMDATAACCESSLib;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Grasshopper_RAM.Utilities
{
    public static class Helpers
    {
        public static List<IFloorType> GetFloorTypes(IModel model, List<string> floorTypeNames)
        {
            List<IFloorType> floorTypes = new List<IFloorType>();
            foreach (string floorTypeName in floorTypeNames)
            {
                for (int i = 0; i < model.GetFloorTypes().GetCount(); i++)
                {
                    IFloorType floorType = model.GetFloorTypes().GetAt(i);
                    if (floorType.strLabel == floorTypeName)
                    {
                        floorTypes.Add(floorType);
                    }
                }
            }
            return floorTypes;
        }
        internal static EMATERIALTYPES GetMaterialType(int material)
        {
            EMATERIALTYPES materialType = EMATERIALTYPES.EConcreteMat;  
            if (material == 0)
            {
                materialType = EMATERIALTYPES.EConcreteMat;
            }
            else if (material == 1)
            {
                materialType = EMATERIALTYPES.ESteelMat;
            }
            else
            {
                throw new Exception("Material type not supported");
            }
            return materialType;
        }

        public static List<List<Line>> ConvertLinesToNestedList(GH_Structure<GH_Line> lines)
        {
            List<List<Line>> nestedList = new List<List<Line>>();

            for (int i = 0; i < lines.PathCount; i++)
            {
                GH_Path path = lines.get_Path(i);
                List<GH_Line> lineBranch = lines.get_Branch(path) as List<GH_Line>;

                List<Line> lineList = lineBranch.Select(line => line.Value).ToList();

                nestedList.Add(lineList);
            }
            return nestedList;
        }

        internal static List<List<Point3d>> GetPoints(GH_Structure<GH_Point> points)
        {
            List<List<Point3d>> nestedList = new List<List<Point3d>>();

            for (int i = 0; i < points.PathCount; i++)
            {
                GH_Path path = points.get_Path(i);
                List<GH_Point> pointBranch = points.get_Branch(path) as List<GH_Point>;
                List<Point3d> pointList = pointBranch.Select(point => point.Value).ToList();
                nestedList.Add(pointList);
            }
            return nestedList;  
        }
    }
}

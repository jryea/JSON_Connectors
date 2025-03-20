using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

using RAMDATAACCESSLib;
using Grasshopper_RAM.Utilities;

namespace JSON_Connectors.Connectors.RAM.Export
{
    public class Stories : GH_Component
    {
        public Stories()
            : base("Stories", "RMST", "Create RAM stories", "SPEED", "RAM")
        {
        }
        public override Guid ComponentGuid => new Guid("0259c49a-5a53-4c54-9c9c-1107d62a3fa0");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FILE", "RAM structural model file name", GH_ParamAccess.item);
            pManager.AddTextParameter("Floor Type", "FT", "RAM floor type", GH_ParamAccess.list);
            pManager.AddNumberParameter("Story Elevations", "SE", "story elevations", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("File Name", "FN", "file name", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Story ID", "SID", "RAM story ID", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get Inputs   
            string fileName = string.Empty;
            List<string> floorTypeNames = new List<string>();
            GH_Structure<GH_Number> storyElevations = new GH_Structure<GH_Number>();

            if (!DA.GetData(0, ref fileName)) return;
            if (!DA.GetDataList(1, floorTypeNames)) return;
            if (!DA.GetDataTree(2, out storyElevations)) return;

            // Open Model and Database
            RamDataAccess1 ramDataAccess = new RamDataAccess1();
            IDBIO1 db = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            db.LoadDataBase2(fileName, "1");
            IModel model = ramDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;

            List<IFloorType> floorTypes = Helpers.GetFloorTypes(model, floorTypeNames);
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"floorTypes count: {floorTypes.Count}");

            List<int> storyIds = CreateStories(model, floorTypes, storyElevations);

            db.SaveDatabase();
            db.CloseDatabase();

            DA.SetData(0, fileName);
            DA.SetDataList(1, storyIds);
        }

        private List<int> CreateStories(IModel model, List<IFloorType> floorTypes, GH_Structure<GH_Number> storyElevations)
        {
            // Get Stories
            IStories stories = model.GetStories();
            List<int> storyIds = new List<int>();
            List<List<double>> storyElevationsList = ConvertToNestedList(storyElevations);
            int storyCount = 1;
            double totalStoryHeight = 0;

            for (int i = 0; i < floorTypes.Count; i++)
            {
                IFloorType floorType = floorTypes[i];
                List<double> elevations = storyElevationsList[i];

                for (int j = 0; j < elevations.Count; j++)
                {
                    double storyHeight = elevations[j] * 12 - totalStoryHeight;
                    totalStoryHeight += storyHeight;

                    string storyName = $"Story {storyCount++}";

                    IStory story = stories.Add(floorType.lUID, storyName, storyHeight);
                    storyIds.Add(story.lUID);
                }
            }
            return storyIds;
        }

        private List<List<double>> ConvertToNestedList(GH_Structure<GH_Number> storyElevations)
        {
            List<List<double>> nestedList = new List<List<double>>();

            for (int i = 0; i < storyElevations.PathCount; i++)
            {
                GH_Path path = storyElevations.get_Path(i);
                List<GH_Number> numbers = storyElevations.get_Branch(path) as List<GH_Number>;

                var storyList = new List<double>();
                foreach (GH_Number number in numbers)
                {
                    storyList.Add((double)number.Value);
                }
                nestedList.Add(storyList);
            }
            return nestedList;
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

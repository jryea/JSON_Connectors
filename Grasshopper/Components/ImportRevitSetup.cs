using Core.Models.Metadata;
using Core.Models;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Collections.Generic;
using System.IO;
using System;
using Grasshopper.Components.Core;
using Grasshopper.Components.Core.Import;

public class ImportRevitSetupComponent : ComponentBase
{
    public ImportRevitSetupComponent()
        : base("Import Revit Model", "RevitImport",
            "Import a Revit model exported to JSON",
            "IMEG", "Import")
    {
    }

    public override Guid ComponentGuid => throw new NotImplementedException();

    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Folder Path", "P", "Path to the exported model folder", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Consolidate Floors", "CF", "Consolidate floors by level", GH_ParamAccess.item, true);
    }

    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
        pManager.AddGeometryParameter("Grids", "G", "Grid lines", GH_ParamAccess.list);
        pManager.AddGeometryParameter("Levels", "L", "Level planes", GH_ParamAccess.list);
        pManager.AddGeometryParameter("Floors", "F", "Floor boundaries", GH_ParamAccess.list);
        pManager.AddGenericParameter("CAD Plans", "C", "CAD plan files", GH_ParamAccess.list);
        pManager.AddGenericParameter("Model Data", "D", "Complete model data", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        string folderPath = "";
        bool consolidateFloors = true;

        if (!DA.GetData(0, ref folderPath)) return;
        DA.GetData(1, ref consolidateFloors);

        // Create importer and load model
        RevitSetupImporter importer = new RevitSetupImporter(folderPath);
        if (!importer.LoadModel())
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to load model from JSON");
            return;
        }

        // Optionally consolidate floors
        if (consolidateFloors)
        {
            importer.ConsolidateFloorsByLevel();
        }

        // Get model and transform to Rhino geometry
        BaseModel model = importer.GetModel();
        List<string> cadFiles = importer.GetCADFiles();

        // Create a coordinate transformer
        CoordinateTransformer transformer = new CoordinateTransformer(model);

        // Create geometries
        List<Line> gridLines = transformer.CreateGridGeometry();
        List<Rectangle3d> levelPlanes = transformer.CreateLevelGeometry();
        List<Curve> floorCurves = transformer.CreateFloorGeometry();

        // Set outputs
        DA.SetDataList(0, gridLines);
        DA.SetDataList(1, levelPlanes);
        DA.SetDataList(2, floorCurves);
        DA.SetDataList(3, cadFiles);
        DA.SetData(4, model);
    }
}
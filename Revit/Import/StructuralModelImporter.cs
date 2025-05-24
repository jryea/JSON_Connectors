using System;
using System.Diagnostics;
using Core.Models;

namespace Revit.Import
{
    // Main orchestrator for structural model import
    // Clean separation: Load -> Transform -> Filter
    public class StructuralModelImporter
    {
        public BaseModel Import(ImportContext context)
        {
            Debug.WriteLine("StructuralModelImporter: Starting import");

            try
            {
                // 1. Load model from file (handles format conversion)
                Debug.WriteLine("StructuralModelImporter: Loading model...");
                var loader = new StructuralModelLoader(context);
                var model = loader.LoadModel();
                Debug.WriteLine("StructuralModelImporter: Model loaded successfully");

                // 2. Apply transformations
                Debug.WriteLine("StructuralModelImporter: Applying transformations...");
                var transformer = new ModelTransformer(context);
                transformer.TransformModel(model);
                Debug.WriteLine("StructuralModelImporter: Transformations applied");

                // 3. Apply filters to remove unwanted elements/materials
                Debug.WriteLine("StructuralModelImporter: Applying filters...");
                var filter = new ImportModelFilter(context);
                filter.FilterModel(model);
                Debug.WriteLine("StructuralModelImporter: Filters applied");

                Debug.WriteLine("StructuralModelImporter: Import complete");
                return model;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StructuralModelImporter: Error during import: {ex.Message}");
                Debug.WriteLine($"StructuralModelImporter: Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
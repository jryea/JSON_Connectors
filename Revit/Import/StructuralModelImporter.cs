using System;
using System.Diagnostics;
using Core.Models;

namespace Revit.Import
{
    /// <summary>
    /// Main orchestrator for structural model import
    /// Clean separation: Load -> Transform -> Filter
    /// </summary>
    public class StructuralModelImporter
    {
        public BaseModel Import(ImportContext context)
        {
            Debug.WriteLine("StructuralModelImporter: Starting import");

            try
            {
                // 1. Load model from file (handles format conversion)
                var loader = new StructuralModelLoader(context);
                var model = loader.LoadModel();

                // 2. Apply transformations
                var transformer = new ModelTransformer(context);
                transformer.TransformModel(model);

                // 3. Apply filters (if needed - may not be necessary for import)
                // var filter = new ImportModelFilter(context);
                // filter.FilterModel(model);

                Debug.WriteLine("StructuralModelImporter: Import complete");
                return model;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StructuralModelImporter: Error during import: {ex.Message}");
                throw;
            }
        }
    }
}
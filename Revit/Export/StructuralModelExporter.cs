using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Core.Models;

namespace Revit.Export
{
    /// <summary>
    /// Main orchestrator for structural model export
    /// Clean separation: Build -> Filter -> Output
    /// </summary>
    public class StructuralModelExporter
    {
        public BaseModel Export(ExportContext context)
        {
            Debug.WriteLine("StructuralModelExporter: Starting export");

            try
            {
                // 1. Build complete model (no filtering)
                var builder = new StructuralModelBuilder(context);
                var model = builder.BuildModel();

                // 2. Apply filters to complete model
                var filter = new ModelFilter(context);
                filter.FilterModel(model);

                Debug.WriteLine("StructuralModelExporter: Export complete");
                return model;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StructuralModelExporter: Error during export: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates export context from legacy parameters for backward compatibility
        /// </summary>
        public static ExportContext CreateContext(Document doc,
            Dictionary<string, bool> elementFilters = null,
            Dictionary<string, bool> materialFilters = null,
            List<ElementId> selectedLevelIds = null,
            ElementId baseLevelId = null,
            List<Core.Models.ModelLayout.FloorType> customFloorTypes = null,
            List<Core.Models.ModelLayout.Level> customLevels = null)
        {
            var context = new ExportContext(doc)
            {
                ElementFilters = elementFilters ?? new Dictionary<string, bool>(),
                MaterialFilters = materialFilters ?? new Dictionary<string, bool>(),
                SelectedLevelIds = selectedLevelIds ?? new List<ElementId>(),
                BaseLevelId = baseLevelId,
                CustomFloorTypes = customFloorTypes,
                CustomLevels = customLevels
            };

            return context;
        }
    }
}
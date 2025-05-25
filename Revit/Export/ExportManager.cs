using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;

namespace Revit.Export
{
    public class ExportManager
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;

        public ExportManager(Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
        }

        /// <summary>
        /// Keep existing method signature but clean up implementation
        /// Creates ONE JSON file, converts to target format
        /// </summary>
        public int ExportToJson(string filePath)
        {
            // Create default options for simple JSON export
            var options = ExportOptions.CreateDefault(filePath, ExportFormat.Grasshopper);

            // Use cleaned UnifiedExporter
            var exporter = new UnifiedExporter(_doc, options.ElementFilters, options.MaterialFilters);
            var result = exporter.Export(options);

            if (!result.Success)
                throw new Exception(result.ErrorMessage);

            return result.ElementCount;
        }

        /// <summary>
        /// Keep existing method signature but use cleaned UnifiedExporter
        /// </summary>
        public int ExportToJson(string filePath, Dictionary<string, bool> elementFilters,
                       Dictionary<string, bool> materialFilters,
                       List<ElementId> selectedLevelIds = null,
                       ElementId baseLevelId = null,
                       List<Core.Models.ModelLayout.FloorType> customFloorTypes = null,
                       List<Core.Models.ModelLayout.Level> customLevels = null)
        {
            try
            {
                // Create export options
                var options = new ExportOptions
                {
                    OutputPath = filePath,
                    Format = ExportFormat.Grasshopper, // JSON format
                    ElementFilters = elementFilters,
                    MaterialFilters = materialFilters,
                    SelectedLevels = selectedLevelIds,
                    BaseLevel = baseLevelId != null ? _doc.GetElement(baseLevelId) as Level : null,
                    CustomFloorTypes = customFloorTypes,
                    CustomLevels = customLevels
                };

                // Use cleaned UnifiedExporter - creates ONE JSON file
                var exporter = new UnifiedExporter(_doc, elementFilters, materialFilters);
                var result = exporter.Export(options);

                if (!result.Success)
                    throw new Exception(result.ErrorMessage);

                return result.ElementCount;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Export Error", $"Error exporting model: {ex.Message}");
                return 0;
            }
        }
    }
}
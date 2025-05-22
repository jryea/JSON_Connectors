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

        public int ExportToJson(string filePath)
        {
            // Create default filters (all enabled)
            Dictionary<string, bool> elementFilters = new Dictionary<string, bool>
            {
                { "Grids", true },
                { "Beams", true },
                { "Braces", true },
                { "Columns", true },
                { "Floors", true },
                { "Walls", true },
                { "Footings", true }
            };

            Dictionary<string, bool> materialFilters = new Dictionary<string, bool>
            {
                { "Steel", true },
                { "Concrete", true }
            };

            return ExportToJson(filePath, elementFilters, materialFilters, null, null);
        }

        public int ExportToJson(string filePath, Dictionary<string, bool> elementFilters,
                       Dictionary<string, bool> materialFilters,
                       List<ElementId> selectedLevelIds = null,
                       ElementId baseLevelId = null,
                       List<Core.Models.ModelLayout.FloorType> customFloorTypes = null,
                       List<Core.Models.ModelLayout.Level> customLevels = null)
        {
            try
            {
                // Create export context
                var context = StructuralModelExporter.CreateContext(_doc,
                    elementFilters, materialFilters, selectedLevelIds, baseLevelId,
                    customFloorTypes, customLevels);

                // Use new clean architecture
                var exporter = new StructuralModelExporter();
                var model = exporter.Export(context);

                // Save the model to file
                JsonConverter.SaveToFile(model, filePath);

                // Calculate total exported elements for backward compatibility
                int totalExported = CalculateExportedCount(model);

                return totalExported;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Export Error", $"Error exporting model: {ex.Message}");
                return 0;
            }
        }

        private int CalculateExportedCount(BaseModel model)
        {
            int count = 0;

            if (model.ModelLayout != null)
            {
                count += model.ModelLayout.Levels?.Count ?? 0;
                count += model.ModelLayout.Grids?.Count ?? 0;
                count += model.ModelLayout.FloorTypes?.Count ?? 0;
            }

            if (model.Properties != null)
            {
                count += model.Properties.Materials?.Count ?? 0;
                count += model.Properties.WallProperties?.Count ?? 0;
                count += model.Properties.FloorProperties?.Count ?? 0;
                count += model.Properties.FrameProperties?.Count ?? 0;
            }

            if (model.Elements != null)
            {
                count += model.Elements.Walls?.Count ?? 0;
                count += model.Elements.Floors?.Count ?? 0;
                count += model.Elements.Columns?.Count ?? 0;
                count += model.Elements.Beams?.Count ?? 0;
                count += model.Elements.Braces?.Count ?? 0;
                count += model.Elements.IsolatedFootings?.Count ?? 0;
            }

            return count;
        }
    }
}
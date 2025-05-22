using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit.Export
{
    // Single source of truth for export parameters and computed mappings
    public class ExportContext
    {
        public Document RevitDoc { get; set; }
        public List<ElementId> SelectedLevelIds { get; set; }
        public ElementId BaseLevelId { get; set; }
        public Dictionary<string, bool> ElementFilters { get; set; }
        public Dictionary<string, bool> MaterialFilters { get; set; }

        // Custom data for Grasshopper/RAM exports
        public List<Core.Models.ModelLayout.FloorType> CustomFloorTypes { get; set; }
        public List<Core.Models.ModelLayout.Level> CustomLevels { get; set; }

        // Computed once, used everywhere
        public Dictionary<ElementId, Level> RevitLevels { get; private set; }
        public Dictionary<ElementId, string> LevelIdMapping { get; private set; }
        public Dictionary<string, ElementId> ReverseLevelIdMapping { get; private set; }
        public double BaseLevelElevation { get; private set; }
        public Level BaseLevel { get; private set; }

        public ExportContext(Document doc)
        {
            RevitDoc = doc;
            ElementFilters = new Dictionary<string, bool>();
            MaterialFilters = new Dictionary<string, bool>();
            SelectedLevelIds = new List<ElementId>();

            InitializeLevelMappings();
        }

        private void InitializeLevelMappings()
        {
            RevitLevels = new Dictionary<ElementId, Level>();
            LevelIdMapping = new Dictionary<ElementId, string>();
            ReverseLevelIdMapping = new Dictionary<string, ElementId>();

            // Get all levels from Revit
            var collector = new FilteredElementCollector(RevitDoc);
            var levels = collector.OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            foreach (var level in levels)
            {
                RevitLevels[level.Id] = level;

                // Generate consistent model level ID
                string modelLevelId = Core.Utilities.IdGenerator.Generate(Core.Utilities.IdGenerator.Layout.LEVEL);
                LevelIdMapping[level.Id] = modelLevelId;
                ReverseLevelIdMapping[modelLevelId] = level.Id;
            }

            // Set base level info if specified
            if (BaseLevelId != null && RevitLevels.ContainsKey(BaseLevelId))
            {
                BaseLevel = RevitLevels[BaseLevelId];
                BaseLevelElevation = BaseLevel.Elevation;
            }
        }

        public bool ShouldExportElement(string elementType)
        {
            return ElementFilters.ContainsKey(elementType) && ElementFilters[elementType];
        }

        public bool ShouldExportMaterial(string materialType)
        {
            return MaterialFilters.ContainsKey(materialType) && MaterialFilters[materialType];
        }

        public bool IsLevelSelected(ElementId levelId)
        {
            return SelectedLevelIds == null || SelectedLevelIds.Count == 0 || SelectedLevelIds.Contains(levelId);
        }

        public string GetModelLevelId(ElementId revitLevelId)
        {
            return LevelIdMapping.ContainsKey(revitLevelId) ? LevelIdMapping[revitLevelId] : null;
        }

        public ElementId GetRevitLevelId(string modelLevelId)
        {
            return ReverseLevelIdMapping.ContainsKey(modelLevelId) ? ReverseLevelIdMapping[modelLevelId] : null;
        }
    }
}
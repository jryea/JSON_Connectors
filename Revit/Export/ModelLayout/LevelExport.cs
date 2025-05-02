using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CG = Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Utilities;
using System.Diagnostics;

namespace Revit.Export.ModelLayout
{
    public class LevelExport
    {
        private readonly DB.Document _doc;

        public LevelExport(DB.Document doc)
        {
            _doc = doc;
        }

        // Modified to support filtering by level IDs
        public int Export(List<Level> levels, List<DB.ElementId> selectedLevelIds = null)
        {
            int count = 0;

            // Get all levels from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Level> revitLevels = collector.OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .OrderBy(l => l.ProjectElevation)
                .ToList();

            // Filter levels by selected IDs if applicable
            if (selectedLevelIds != null && selectedLevelIds.Count > 0)
            {
                revitLevels = revitLevels.Where(l => selectedLevelIds.Contains(l.Id)).ToList();
                Debug.WriteLine($"Filtering levels to {revitLevels.Count} selected levels");
            }

            // Process each level
            foreach (var revitLevel in revitLevels)
            {
                try
                {
                    // Create level object with proper ID
                    Level level = new Level
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.LEVEL),
                        Name = revitLevel.Name,
                        Elevation = revitLevel.ProjectElevation * 12.0 // Convert feet to inches
                        // FloorTypeId will be set later in the mapping process
                    };

                    levels.Add(level);
                    count++;

                    Debug.WriteLine($"Exported level {level.Name} with elevation {level.Elevation}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting level {revitLevel.Name}: {ex.Message}");
                    // Skip this level and continue with the next one
                }
            }

            return count;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CG = Core.Models.Geometry;
using Core.Models.ModelLayout;
using Revit.Utilities;

namespace Revit.Export.ModelLayout
{
    public class LevelExport
    {
        private readonly DB.Document _doc;

        public LevelExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<Level> levels)
        {
            int count = 0;

            // Get all levels from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Level> revitLevels = collector.OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            foreach (var revitLevel in revitLevels)
            {
                try
                {
                    // Create level object
                    Level level = new Level
                    {
                        Name = revitLevel.Name,
                        Elevation = revitLevel.Elevation * 12.0, // Convert feet to inches
                        FloorTypeId = "FT-default" // Default floor type ID, can be updated later
                    };

                    levels.Add(level);
                    count++;
                }
                catch (Exception)
                {
                    // Skip this level and continue with the next one
                }
            }

            return count;
        }
    }
}
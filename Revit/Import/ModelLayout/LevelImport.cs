using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using CL = Core.Models.ModelLayout;
using Revit.Utilities;
using System.Linq;

namespace Revit.Import.ModelLayout
{
    // Imports level elements from JSON into Revit
    public class LevelImport
    {
        private readonly DB.Document _doc;

        public LevelImport(DB.Document doc)
        {
            _doc = doc;
        }

        // Imports levels from the JSON model into Revit
        public int Import(List<CL.Level> levels, Dictionary<string, DB.ElementId> levelMapping)
        {
            int count = 0;

            // Get all existing Revit levels
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.Level));
            Dictionary<string, DB.Level> existingLevels = collector
                .Cast<DB.Level>()
                .ToDictionary(level => level.Name, level => level);

            for (int i = 0; i < levels.Count; i++)
            {
                var jsonLevel = levels[i];
                try
                {
                    // Determine the desired level name
                    string levelName = $"Level {i}";

                    // Check if the level is already in the mapping
                    if (levelMapping.ContainsKey(jsonLevel.Id))
                    {
                        continue;
                    }

                    // Check if a level with the desired name already exists in Revit
                    if (existingLevels.TryGetValue(levelName, out DB.Level existingLevel))
                    {
                        // Add the existing level to the mapping
                        levelMapping[jsonLevel.Id] = existingLevel.Id;
                        continue;
                    }

                    // Convert elevation from inches to feet for Revit
                    double elevation = jsonLevel.Elevation / 12.0;

                    // Create a new level in Revit
                    DB.Level revitLevel = DB.Level.Create(_doc, elevation);
                    revitLevel.Name = levelName;

                    // Add the new level to the mapping
                    levelMapping[jsonLevel.Id] = revitLevel.Id;

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

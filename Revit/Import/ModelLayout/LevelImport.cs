using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using C = Core.Models.ModelLayout;
using Revit.Utils;

namespace Revit.Import.ModelLayout
{
    /// <summary>
    /// Imports level elements from JSON into Revit
    /// </summary>
    public class LevelImport
    {
        private readonly Document _doc;

        public LevelImport(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Imports levels from the JSON model into Revit
        /// </summary>
        /// <param name="levels">List of levels to import</param>
        /// <param name="levelIdMap">Dictionary to store mappings of JSON IDs to Revit ElementIds</param>
        /// <returns>Number of levels imported</returns>
        public int Import(List<C.Level> levels, Dictionary<string, ElementId> levelIdMap)
        {
            int count = 0;

            foreach (var jsonLevel in levels)
            {
                try
                {
                    // Convert elevation from inches to feet for Revit
                    double elevation = jsonLevel.Elevation / 12.0;

                    // Create level in Revit
                    Autodesk.Revit.DB.Level revitLevel = Level.Create(_doc, elevation);

                    // Set level name
                    revitLevel.Name = jsonLevel.Name;

                    // Store the mapping from JSON ID to Revit ElementId
                    levelIdMap[jsonLevel.Id] = revitLevel.Id;

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this level but continue with the next one
                    Debug.WriteLine($"Error creating level {jsonLevel.Name}: {ex.Message}");
                }
            }

            return count;
        }
    }
}
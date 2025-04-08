using System;
using System.Collections.Generic;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using Core.Models.ModelLayout;
using Revit.Utilities;

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
      
        public int Import(List<Level> levels)
        {
            int count = 0;

            foreach (var jsonLevel in levels)
            {
                try
                {
                    // Convert elevation from inches to feet for Revit
                    double elevation = jsonLevel.Elevation / 12.0;

                    // Create level in Revit
                    DB.Level revitLevel = DB.Level.Create(_doc, elevation);

                    // Set level name
                    string revitLevelName = $"Level {jsonLevel.Name}";
                    revitLevel.Name = revitLevelName;

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
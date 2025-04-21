using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Properties;
using Core.Models.ModelLayout;
using Revit.Import.ModelLayout;
using Revit.Import.Elements;
using System.Linq;
using System.Diagnostics;

namespace Revit.Import
{
    // Manages the import of a JSON structural model into Revit
    public class ImportManager
    {
        private readonly DB.Document _doc;
        private readonly UIApplication _uiApp;
        private readonly Dictionary<string, DB.ElementId> _levelIdMap;
        private readonly Dictionary<string, DB.ElementId> _gridIdMap;
        private readonly Dictionary<string, DB.ElementId> _materialIdMap;
        private BaseModel _currentModel;

        public ImportManager(DB.Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
            _levelIdMap = new Dictionary<string, DB.ElementId>();
            _gridIdMap = new Dictionary<string, DB.ElementId>();
            _materialIdMap = new Dictionary<string, DB.ElementId>();
        }

        // Imports the entire model from JSON file
        public int ImportFromJson(string filePath)
        {
            // Load the model from file
            _currentModel = JsonConverter.LoadFromFile(filePath);
            int totalImported = 0;

            using (DB.Transaction transaction = new DB.Transaction(_doc, "Import Structural Model"))
            {
                transaction.Start();

                try
                {
                    // 1. Create mappings first
                    CreateMappings(_currentModel);

                    // 2. Import layout elements
                    ImportLayoutElements(_currentModel, ref totalImported);

                    // 5. Import structural elements
                    ImportStructuralElements(_currentModel, ref totalImported);

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.RollBack();
                    TaskDialog.Show("Import Error", $"Error importing model: {ex.Message}");
                }
            }

            return totalImported;
        }

        private void CreateMappings(BaseModel model)
        {
            CreateLevelMappings(model.ModelLayout.Levels);
        }

        private void ImportLayoutElements(BaseModel model, ref int totalImported)
        {
            // Import grids
            GridImport gridImport = new GridImport(_doc);
            int gridsImported = gridImport.Import(model.ModelLayout.Grids);
            totalImported += gridsImported;

            // Import levels
            LevelImport levelImport = new LevelImport(_doc);
            int levelsImported = levelImport.Import(model.ModelLayout.Levels, _levelIdMap);
            totalImported += levelsImported;
        }

        private void ImportStructuralElements(BaseModel model, ref int totalImported)
        {
            // Import beams using the new BeamImport class
            if (model.Elements.Beams != null && model.Elements.Beams.Count > 0)
            {
                BeamImport beamImport = new BeamImport(_doc);
                int beamsImported = beamImport.Import(model.Elements.Beams, _levelIdMap, model);
                totalImported += beamsImported;
            }

            // Import braces using the new BraceImport class
            if (model.Elements.Braces != null && model.Elements.Braces.Count > 0)
            {
                BraceImport braceImport = new BraceImport(_doc);
                int bracesImported = braceImport.Import(model.Elements.Braces, _levelIdMap, model);
                totalImported += bracesImported;
            }

            // Import columns
            if (model.Elements.Columns != null && model.Elements.Columns.Count > 0)
            {
                ColumnImport columnImport = new ColumnImport(_doc);
                int columnsImported = columnImport.Import(model.Elements.Columns, _levelIdMap, model);
                totalImported += columnsImported;
            }

            // Import floors
            if (model.Elements.Floors != null && model.Elements.Floors.Count > 0)
            {
                FloorImport floorImport = new FloorImport(_doc);
                int floorsImported = floorImport.Import(_levelIdMap, model);
                totalImported += floorsImported;
            }

            // Import walls
            if (model.Elements.Walls != null && model.Elements.Walls.Count > 0)
            {
                WallImport wallImport = new WallImport(_doc);
                int wallsImported = wallImport.Import(model.Elements.Walls, _levelIdMap, model);
                totalImported += wallsImported;
            }

            // Import isolated footings
            if (model.Elements.IsolatedFootings != null && model.Elements.IsolatedFootings.Count > 0)
            {
                IsolatedFootingImport footingImport = new IsolatedFootingImport(_doc);
                int footingsImported = footingImport.Import(model.Elements.IsolatedFootings, _levelIdMap, model);
                totalImported += footingsImported;
            }
        }

        // Creates mappings between JSON level IDs and Revit level elements
        // For ImportManager.cs
        private string FormatLevelName(string jsonLevelName)
        {
            if (string.IsNullOrEmpty(jsonLevelName))
                return "Level";

            // If the name is only a number, add "Level " prefix
            if (int.TryParse(jsonLevelName, out _))
                return $"Level {jsonLevelName}";

            // If the name contains "story", replace "story" with "level"
            if (jsonLevelName.ToLower().Contains("story"))
                return jsonLevelName.ToLower().Replace("story", "Level");

            // Otherwise, use the name as is
            return jsonLevelName;
        }

        private void CreateLevelMappings(List<Level> levels)
        {
            _levelIdMap.Clear();

            if (levels == null || levels.Count == 0)
                return;

            // Get all existing Revit levels
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.Level));
            List<DB.Level> existingRevitLevels = collector.Cast<DB.Level>().ToList();

            // Create a hash set of existing level names for quick lookup
            HashSet<string> existingLevelNames = new HashSet<string>(existingRevitLevels.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);

            foreach (Level jsonLevel in levels)
            {
                string levelName = FormatLevelName(jsonLevel.Name);

                // Ensure the level name is unique
                while (existingLevelNames.Contains(levelName))
                {
                    levelName += " new";
                }

                try
                {
                    // Create a new level in Revit
                    double elevation = jsonLevel.Elevation / 12.0; // Convert elevation from inches to feet
                    DB.Level newLevel = DB.Level.Create(_doc, elevation);
                    newLevel.Name = levelName;

                    // Add the new level to the mapping
                    _levelIdMap[jsonLevel.Id] = newLevel.Id;
                    existingLevelNames.Add(levelName); // Add the new name to the set
                    Debug.WriteLine($"Created new level '{levelName}' with elevation {elevation}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to create level '{levelName}': {ex.Message}");
                }
            }
        }
    }
}
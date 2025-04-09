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
        private readonly Dictionary<string, DB.ElementId> _floorPropertyIdMap;
        private readonly Dictionary<string, DB.ElementId> _wallPropertyIdMap;
        private BaseModel _currentModel;

        public ImportManager(DB.Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
            _levelIdMap = new Dictionary<string, DB.ElementId>();
            _gridIdMap = new Dictionary<string, DB.ElementId>();
            _materialIdMap = new Dictionary<string, DB.ElementId>();
            _floorPropertyIdMap = new Dictionary<string, DB.ElementId>();
            _wallPropertyIdMap = new Dictionary<string, DB.ElementId>();
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
            CreateFloorPropertyMappings(model.Properties.FloorProperties);
            CreateWallPropertyMappings(model.Properties.WallProperties);
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
            // Import beams and braces with the combined frame element importer
            FrameElementImport frameImport = new FrameElementImport(_doc);

            // Import beams
            if (model.Elements.Beams != null && model.Elements.Beams.Count > 0)
            {
                int beamsImported = frameImport.ImportBeams(model.Elements.Beams, _levelIdMap, model);
                totalImported += beamsImported;
            }

            // Import braces
            if (model.Elements.Braces != null && model.Elements.Braces.Count > 0)
            {
                int bracesImported = frameImport.ImportBraces(model.Elements.Braces, _levelIdMap, model);
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
                int floorsImported = floorImport.Import(model.Elements.Floors, _levelIdMap, _floorPropertyIdMap);
                totalImported += floorsImported;
            }

            // Import walls
            if (model.Elements.Walls != null && model.Elements.Walls.Count > 0)
            {
                WallImport wallImport = new WallImport(_doc);
                int wallsImported = wallImport.Import(model.Elements.Walls, _levelIdMap, _wallPropertyIdMap);
                totalImported += wallsImported;
            }
        }

        // Creates mappings between JSON level IDs and Revit level elements
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
                string levelName = $"Level {jsonLevel.Name}";

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

        // Creates mappings between JSON floor properties and Revit floor types
        private void CreateFloorPropertyMappings(List<FloorProperties> floorProperties)
        {
            _floorPropertyIdMap.Clear();

            if (floorProperties == null || floorProperties.Count == 0)
                return;

            // Collect all floor types
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.FloorType));
            List<DB.FloorType> floorTypes = collector.Cast<DB.FloorType>().ToList();

            // Default floor type
            DB.FloorType defaultFloorType = floorTypes.FirstOrDefault();

            // Map each floor property to a floor type
            foreach (FloorProperties prop in floorProperties)
            {
                // Find a matching floor type by name (if possible)
                DB.FloorType matchedType = floorTypes.FirstOrDefault(ft =>
                    ft.Name.Equals(prop.Name, StringComparison.OrdinalIgnoreCase));

                // If no match, use default
                if (matchedType == null)
                {
                    matchedType = defaultFloorType;
                }

                // Add to mapping
                if (matchedType != null)
                {
                    _floorPropertyIdMap[prop.Id] = matchedType.Id;
                }
            }
        }

        // Creates mappings between JSON wall properties and Revit wall types
        private void CreateWallPropertyMappings(List<WallProperties> wallProperties)
        {
            _wallPropertyIdMap.Clear();

            if (wallProperties == null || wallProperties.Count == 0)
                return;

            // Collect all wall types
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.WallType));
            List<DB.WallType> wallTypes = collector.Cast<DB.WallType>().ToList();

            // Default wall type
            DB.WallType defaultWallType = wallTypes.FirstOrDefault();

            // Map each wall property to a wall type
            foreach (WallProperties prop in wallProperties)
            {
                // Find a matching wall type by name (if possible)
                DB.WallType matchedType = wallTypes.FirstOrDefault(wt =>
                    wt.Name.Equals(prop.Name, StringComparison.OrdinalIgnoreCase));

                // If no match, use default
                if (matchedType == null)
                {
                    matchedType = defaultWallType;
                }

                // Add to mapping
                if (matchedType != null)
                {
                    _wallPropertyIdMap[prop.Id] = matchedType.Id;
                }
            }
        }
    }
}
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
        private readonly Dictionary<string, DB.ElementId> _framePropertyIdMap;
        private readonly Dictionary<string, DB.ElementId> _floorPropertyIdMap;
        private readonly Dictionary<string, DB.ElementId> _wallPropertyIdMap;

        public ImportManager(DB.Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
            _levelIdMap = new Dictionary<string, DB.ElementId>();
            _gridIdMap = new Dictionary<string, DB.ElementId>();
            _materialIdMap = new Dictionary<string, DB.ElementId>();
            _framePropertyIdMap = new Dictionary<string, DB.ElementId>();
            _floorPropertyIdMap = new Dictionary<string, DB.ElementId>();
            _wallPropertyIdMap = new Dictionary<string, DB.ElementId>();
        }

        // Imports the entire model from JSON file
        public int ImportFromJson(string filePath)
        {
            // Load the model from file
            BaseModel model = JsonConverter.LoadFromFile(filePath);
            int totalImported = 0;

            using (DB.Transaction transaction = new DB.Transaction(_doc, "Import Structural Model"))
            {
                transaction.Start();

                try
                {
                    // 1. Create mappings first - this must happen before element creation
                    CreateLevelMappings(model.ModelLayout.Levels);
                    CreateFramePropertyMappings(model.Properties.FrameProperties);

                    // 2. Import layout elements
                    GridImport gridImport = new GridImport(_doc);
                    int gridsImported = gridImport.Import(model.ModelLayout.Grids);
                    totalImported += gridsImported;

                    LevelImport levelImport = new LevelImport(_doc);
                    int levelsImported = levelImport.Import(model.ModelLayout.Levels);
                    totalImported += levelsImported;

                    // 3. Import structural elements
                    if (model.Elements.Beams != null && model.Elements.Beams.Count > 0)
                    {
                        BeamImport beamImport = new BeamImport(_doc);
                        int beamsImported = beamImport.Import(model.Elements.Beams, _levelIdMap, _framePropertyIdMap);
                        totalImported += beamsImported;
                    }

                    // Add other element imports as needed
                    // if (model.Elements.Columns != null && model.Elements.Columns.Count > 0)
                    // {
                    //     ColumnImport columnImport = new ColumnImport(_doc);
                    //     int columnsImported = columnImport.Import(model.Elements.Columns, _levelIdMap, _framePropertyIdMap);
                    //     totalImported += columnsImported;
                    // }

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

        // Creates mappings between JSON level IDs and Revit level elements
        private void CreateLevelMappings(List<Level> levels)
        {
            _levelIdMap.Clear();

            if (levels == null || levels.Count == 0)
                return;

            // Get all existing Revit levels
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.Level));
            List<DB.Level> revitLevels = collector.Cast<DB.Level>().ToList();

            // Create a dictionary of levels by elevation for fast lookup
            Dictionary<double, DB.Level> revitLevelsByElevation = new Dictionary<double, DB.Level>();
            foreach (DB.Level level in revitLevels)
            {
                double elevation = Math.Round(level.Elevation, 4);
                if (!revitLevelsByElevation.ContainsKey(elevation))
                {
                    revitLevelsByElevation[elevation] = level;
                }
            }

            // Create levels if they don't exist, and map them
            foreach (Level jsonLevel in levels)
            {
                // Convert elevation from inches to feet for Revit
                double elevation = Math.Round(jsonLevel.Elevation / 12.0, 4);

                // Try to find existing level by elevation
                if (revitLevelsByElevation.ContainsKey(elevation))
                {
                    // Found level with matching elevation
                    _levelIdMap[jsonLevel.Id] = revitLevelsByElevation[elevation].Id;
                }
                else
                {
                    // No matching level, create a new one
                    DB.Level newLevel = DB.Level.Create(_doc, elevation);
                    newLevel.Name = $"Level {jsonLevel.Name}";
                    _levelIdMap[jsonLevel.Id] = newLevel.Id;

                    // Add to our lookup dictionary
                    revitLevelsByElevation[elevation] = newLevel;
                }
            }
        }

        // Creates mappings between JSON frame properties and Revit family symbols
        private void CreateFramePropertyMappings(List<FrameProperties> frameProperties)
        {
            _framePropertyIdMap.Clear();

            if (frameProperties == null || frameProperties.Count == 0)
                return;

            // Collect all structural framing family symbols
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.FamilySymbol));
            collector.OfCategory(DB.BuiltInCategory.OST_StructuralFraming);

            // Create mapping dictionaries for family symbols
            Dictionary<string, DB.FamilySymbol> wShapes = new Dictionary<string, DB.FamilySymbol>();
            Dictionary<string, DB.FamilySymbol> hss = new Dictionary<string, DB.FamilySymbol>();
            Dictionary<string, DB.FamilySymbol> channels = new Dictionary<string, DB.FamilySymbol>();
            Dictionary<string, DB.FamilySymbol> angles = new Dictionary<string, DB.FamilySymbol>();
            DB.FamilySymbol defaultBeamType = null;

            // Sort family symbols by type
            foreach (DB.FamilySymbol symbol in collector)
            {
                // Set default beam type if not set
                if (defaultBeamType == null)
                {
                    defaultBeamType = symbol;
                }

                string familyName = symbol.Family.Name.ToUpper();
                string symbolName = symbol.Name.ToUpper();

                // Activate the symbol if it isn't already
                if (!symbol.IsActive)
                {
                    symbol.Activate();
                }

                // Categorize by family/symbol name
                if (familyName.Contains("W") || symbolName.Contains("W"))
                {
                    wShapes[symbolName] = symbol;
                }
                else if (familyName.Contains("HSS") || symbolName.Contains("HSS") ||
                         familyName.Contains("TUBE") || symbolName.Contains("TUBE"))
                {
                    hss[symbolName] = symbol;
                }
                else if (familyName.Contains("C") || symbolName.Contains("C") ||
                         familyName.Contains("CHANNEL") || symbolName.Contains("CHANNEL"))
                {
                    channels[symbolName] = symbol;
                }
                else if (familyName.Contains("L") || symbolName.Contains("L") ||
                         familyName.Contains("ANGLE") || symbolName.Contains("ANGLE"))
                {
                    angles[symbolName] = symbol;
                }
            }

            // Map JSON frame properties to Revit family symbols
            foreach (FrameProperties prop in frameProperties)
            {
                DB.FamilySymbol matchedSymbol = null;

                // Try to match by shape
                if (!string.IsNullOrEmpty(prop.Shape))
                {
                    switch (prop.Shape.ToUpper())
                    {
                        case "W":
                            matchedSymbol = FindBestMatch(wShapes, prop);
                            break;
                        case "HSS":
                            matchedSymbol = FindBestMatch(hss, prop);
                            break;
                        case "C":
                            matchedSymbol = FindBestMatch(channels, prop);
                            break;
                        case "L":
                            matchedSymbol = FindBestMatch(angles, prop);
                            break;
                    }
                }

                // If no match found, use default
                if (matchedSymbol == null)
                {
                    matchedSymbol = defaultBeamType;
                }

                // Add to mapping
                if (matchedSymbol != null)
                {
                    _framePropertyIdMap[prop.Id] = matchedSymbol.Id;
                }
            }
        }

        // Find the best matching family symbol for a frame property
        private DB.FamilySymbol FindBestMatch(Dictionary<string, DB.FamilySymbol> symbols, FrameProperties prop)
        {
            if (symbols.Count == 0)
                return null;

            // For now, just return the first symbol as a basic implementation
            // This can be enhanced to match by dimensions in the future
            return symbols.Values.FirstOrDefault();
        }
    }
}
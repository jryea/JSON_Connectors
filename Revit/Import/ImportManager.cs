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
                    // 1. Create mappings first
                    CreateMappings(model);

                    // 2. Import layout elements
                    ImportLayoutElements(model, ref totalImported);

                    // 3. Force regeneration
                    _doc.Regenerate();

                    // 4. Refresh mappings
                    CreateMappings(model);

                    // 5. Import structural elements
                    ImportStructuralElements(model, ref totalImported);

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
            CreateFramePropertyMappings(model.Properties.FrameProperties);
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
            // Import beams
            //if (model.Elements.Beams != null && model.Elements.Beams.Count > 0)
            //{
            //    BeamImport beamImport = new BeamImport(_doc);
            //    int beamsImported = beamImport.Import(model.Elements.Beams, _levelIdMap, _framePropertyIdMap);
            //    totalImported += beamsImported;
            //}

            // Import columns
            if (model.Elements.Columns != null && model.Elements.Columns.Count > 0)
            {
                ColumnImport columnImport = new ColumnImport(_doc);
                int columnsImported = columnImport.Import(model.Elements.Columns, _levelIdMap, _framePropertyIdMap);
                totalImported += columnsImported;
            }

            // Import braces
            //if (model.Elements.Braces != null && model.Elements.Braces.Count > 0)
            //{
            //    BraceImport braceImport = new BraceImport(_doc);
            //    int bracesImported = braceImport.Import(model.Elements.Braces, _levelIdMap, _framePropertyIdMap);
            //    totalImported += bracesImported;
            //}

            // Import floors
            //if (model.Elements.Floors != null && model.Elements.Floors.Count > 0)
            //{
            //    FloorImport floorImport = new FloorImport(_doc);
            //    int floorsImported = floorImport.Import(model.Elements.Floors, _levelIdMap, _floorPropertyIdMap);
            //    totalImported += floorsImported;
            //}

            // Import walls
            //if (model.Elements.Walls != null && model.Elements.Walls.Count > 0)
            //{
            //    WallImport wallImport = new WallImport(_doc);
            //    int wallsImported = wallImport.Import(model.Elements.Walls, _levelIdMap, _wallPropertyIdMap);
            //    totalImported += wallsImported;
            //}
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

            // Map JSON level IDs to Revit level ElementIds
            foreach (Level jsonLevel in levels)
            {
                string levelName = $"Level {jsonLevel.Name}";

                // Try to find existing level by name
                DB.Level revitLevel = revitLevels.FirstOrDefault(l => l.Name == levelName);
                if (revitLevel != null)
                {
                    _levelIdMap[jsonLevel.Id] = revitLevel.Id;
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

            // Also look for column family symbols
            DB.FilteredElementCollector columnCollector = new DB.FilteredElementCollector(_doc);
            columnCollector.OfClass(typeof(DB.FamilySymbol));
            columnCollector.OfCategory(DB.BuiltInCategory.OST_StructuralColumns);

            foreach (DB.FamilySymbol symbol in columnCollector)
            {
                if (!symbol.IsActive) symbol.Activate();

                string familyName = symbol.Family.Name.ToUpper();
                string symbolName = symbol.Name.ToUpper();

                if (familyName.Contains("W") || symbolName.Contains("W"))
                {
                    wShapes[symbolName] = symbol;
                }
                else if (familyName.Contains("HSS") || symbolName.Contains("HSS"))
                {
                    hss[symbolName] = symbol;
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
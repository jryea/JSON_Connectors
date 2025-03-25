using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Revit.Import.ModelLayout;
using Revit.Import.Elements;

namespace Revit.Import
{
    /// <summary>
    /// Manages the import of a JSON structural model into Revit
    /// </summary>
    public class ImportManager
    {
        private readonly DB.Document _doc;
        private readonly UIApplication _uiApp;
        private readonly Dictionary<string, ElementId> _levelIdMap;
        private readonly Dictionary<string, ElementId> _gridIdMap;
        private readonly Dictionary<string, ElementId> _materialIdMap;
        private readonly Dictionary<string, ElementId> _framePropertyIdMap;
        private readonly Dictionary<string, ElementId> _floorPropertyIdMap;
        private readonly Dictionary<string, ElementId> _wallPropertyIdMap;

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

        /// <summary>
        /// Imports the entire model from JSON file
        /// </summary>
        /// <param name="filePath">Path to the JSON file</param>
        /// <returns>Number of total elements imported</returns>
        public int ImportFromJson(string filePath)
        {
            // Load the model from file
            BaseModel model = JsonConverter.LoadFromFile(filePath);
            int totalImported = 0;

            using (Transaction transaction = new Transaction(_doc, "Import Structural Model"))
            {
                transaction.Start();

                // Import in the right order to handle dependencies

                // 1. First import grids and levels (layout)
                totalImported += ImportGrids(model.ModelLayout.Grids);
                totalImported += ImportLevels(model.ModelLayout.Levels);

                // 2. Import materials and properties
                ImportMaterials(model.Properties.Materials);
                ImportFrameProperties(model.Properties.FrameProperties);
                ImportFloorProperties(model.Properties.FloorProperties);
                ImportWallProperties(model.Properties.WallProperties);

                // 3. Import structural elements
                totalImported += ImportColumns(model.Elements.Columns);
                totalImported += ImportBeams(model.Elements.Beams);
                totalImported += ImportBraces(model.Elements.Braces);
                totalImported += ImportWalls(model.Elements.Walls);
                totalImported += ImportFloors(model.Elements.Floors);
                totalImported += ImportFootings(model.Elements.IsolatedFootings);

                transaction.Commit();
            }

            return totalImported;
        }

        #region Import Methods

        private int ImportGrids(List<Grid> grids)
        {
            GridImport importer = new Importers.GridImporter(_doc);
            return importer.Import(grids, _gridIdMap);
        }

        private int ImportLevels(List<Level> levels)
        {
            Importers.LevelImporter importer = new Importers.LevelImporter(_doc);
            return importer.Import(levels, _levelIdMap);
        }

        private void ImportMaterials(List<Properties.Material> materials)
        {
            // Implementation similar to other importers
        }

        private void ImportFrameProperties(List<Properties.FrameProperties> frameProperties)
        {
            // Implementation similar to other importers
        }

        private void ImportFloorProperties(List<Properties.FloorProperties> floorProperties)
        {
            // Implementation similar to other importers
        }

        private void ImportWallProperties(List<Properties.WallProperties> wallProperties)
        {
            // Implementation similar to other importers
        }

        private int ImportColumns(List<Column> columns)
        {
            Importers.ColumnImporter importer = new Importers.ColumnImporter(_doc);
            return importer.Import(columns, _levelIdMap, _framePropertyIdMap);
        }

        private int ImportBeams(List<Beam> beams)
        {
            Importers.BeamImporter importer = new Importers.BeamImporter(_doc);
            return importer.Import(beams, _levelIdMap, _framePropertyIdMap);
        }

        private int ImportBraces(List<Brace> braces)
        {
            Importers.BraceImporter importer = new Importers.BraceImporter(_doc);
            return importer.Import(braces, _levelIdMap, _framePropertyIdMap);
        }

        private int ImportWalls(List<Wall> walls)
        {
            Importers.WallImporter importer = new Importers.WallImporter(_doc);
            return importer.Import(walls, _wallPropertyIdMap);
        }

        private int ImportFloors(List<Floor> floors)
        {
            Importers.FloorImporter importer = new Importers.FloorImporter(_doc);
            return importer.Import(floors, _levelIdMap, _floorPropertyIdMap);
        }

        private int ImportFootings(List<IsolatedFooting> footings)
        {
            Importers.FootingImporter importer = new Importers.FootingImporter(_doc);
            return importer.Import(footings);
        }

        #endregion
    }
}
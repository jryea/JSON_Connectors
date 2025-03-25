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

namespace Revit.Import
{
    /// <summary>
    /// Manages the import of a JSON structural model into Revit
    /// </summary>
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

            using (DB.Transaction transaction = new DB. Transaction(_doc, "Import Structural Model"))
            {
                transaction.Start();

                // Import in the right order to handle dependencies

                // 1. First import grids and levels (layout)
                totalImported += ImportGrids(model.ModelLayout.Grids);
                totalImported += ImportLevels(model.ModelLayout.Levels);

                // 3. Import structural elements
                totalImported += ImportColumns(model.Elements.Columns);
                totalImported += ImportBeams(model.Elements.Beams);
                totalImported += ImportBraces(model.Elements.Braces);
                totalImported += ImportWalls(model.Elements.Walls);
                totalImported += ImportFloors(model.Elements.Floors);
                totalImported += ImportIsolatedFootings(model.Elements.IsolatedFootings);

                transaction.Commit();
            }

            return totalImported;
        }

        #region Import Methods

        private int ImportGrids(List<Grid> grids)
        {
            GridImport importer = new GridImport(_doc);
            return importer.Import(grids, _gridIdMap);
        }

        private int ImportLevels(List<Level> levels)
        {
            LevelImport importer = new LevelImport(_doc);
            return importer.Import(levels, _levelIdMap);
        }

        private int ImportColumns(List<Column> columns)
        {
            ColumnImport importer = new ColumnImport(_doc);
            return importer.Import(columns, _levelIdMap, _framePropertyIdMap);
        }

        private int ImportBeams(List<Beam> beams)
        {
            BeamImport importer = new BeamImport(_doc);
            return importer.Import(beams, _levelIdMap, _framePropertyIdMap);
        }

        private int ImportBraces(List<Brace> braces)
        {
            BraceImport importer = new BraceImport(_doc);
            return importer.Import(braces, _levelIdMap, _framePropertyIdMap);
        }

        private int ImportWalls(List<Wall> walls)
        {
            WallImport importer = new WallImport(_doc);
            return importer.Import(walls, _wallPropertyIdMap);
        }

        private int ImportFloors(List<Floor> floors)
        {
            FloorImport importer = new FloorImport(_doc);
            return importer.Import(floors, _levelIdMap, _floorPropertyIdMap);
        }

        private int ImportIsolatedFootings(List<IsolatedFooting> footings)
        {
            IsolatedFootingImport importer = new IsolatedFootingImport(_doc);
            return importer.Import(footings);
        }

        #endregion
    }
}
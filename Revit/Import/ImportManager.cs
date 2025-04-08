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

        // Imports the entire model from JSON file
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
                GridImport gridImport = new GridImport(_doc);
                totalImported += gridImport.Import(model.ModelLayout.Grids);

                LevelImport levelImport = new LevelImport(_doc);
                totalImported += levelImport.Import(model.ModelLayout.Levels);

                // 3. Import structural elements
                //totalImported += ImportColumns(model.Elements.Columns);s
                //totalImported += ImportBeams(model.Elements.Beams);
                //totalImported += ImportBraces(model.Elements.Braces);
                //totalImported += ImportWalls(model.Elements.Walls);
                //totalImported += ImportFloors(model.Elements.Floors);
                //totalImported += ImportIsolatedFootings(model.Elements.IsolatedFootings);

                transaction.Commit();
            }

            return totalImported;
        }

    }
}
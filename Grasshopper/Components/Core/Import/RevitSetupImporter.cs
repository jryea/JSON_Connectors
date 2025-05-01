using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Converters;
using Core.Models;
using Core.Utilities;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace Grasshopper.Components.Core.Import
{
    internal class RevitSetupImporter
    {
        private BaseModel _model;
        private string _folderPath;
        private string _jsonFilePath;
        private string _cadFolderPath;

        public RevitSetupImporter(string folderPath)
        {
            _folderPath = folderPath;
            _jsonFilePath = Directory.GetFiles(folderPath, "*.json").FirstOrDefault();
            _cadFolderPath = Path.Combine(folderPath, "CAD");
        }

        public bool LoadModel()
        {
            if (string.IsNullOrEmpty(_jsonFilePath) || !File.Exists(_jsonFilePath))
                return false;

            try
            {
                _model = JsonConverter.LoadFromFile(_jsonFilePath);
                return _model != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public BaseModel GetModel()
        {
            return _model;
        }

        public List<string> GetCADFiles()
        {
            List<string> cadFiles = new List<string>();
            if (Directory.Exists(_cadFolderPath))
            {
                cadFiles.AddRange(Directory.GetFiles(_cadFolderPath, "*.dwg"));
            }
            return cadFiles;
        }

        public void ConsolidateFloorsByLevel()
        {
            if (_model?.Elements?.Floors == null) return;

            // Group floors by level
            Dictionary<string, List<Floor>> floorsByLevel = new Dictionary<string, List<Floor>>();

            foreach (var floor in _model.Elements.Floors)
            {
                if (!floorsByLevel.ContainsKey(floor.LevelId))
                    floorsByLevel[floor.LevelId] = new List<Floor>();

                floorsByLevel[floor.LevelId].Add(floor);
            }

            // Replace model's floors with consolidated ones
            List<Floor> consolidatedFloors = new List<Floor>();

            foreach (var levelGroup in floorsByLevel)
            {
                if (levelGroup.Value.Count > 1)
                {
                    // Create a consolidated floor for this level
                    Floor consolidated = MergeFloors(levelGroup.Value);
                    consolidatedFloors.Add(consolidated);
                }
                else if (levelGroup.Value.Count == 1)
                {
                    // Keep single floors as is
                    consolidatedFloors.Add(levelGroup.Value[0]);
                }
            }

            _model.Elements.Floors = consolidatedFloors;
        }

        private Floor MergeFloors(List<Floor> floors)
        {
            // Create a new merged floor
            Floor mergedFloor = new Floor
            {
                Id = IdGenerator.Generate(IdGenerator.Elements.FLOOR),
                LevelId = floors[0].LevelId,
                FloorPropertiesId = floors[0].FloorPropertiesId,
                DiaphragmId = floors[0].DiaphragmId,
                Points = new List<Point2D>()
            };

            // Union all floor perimeters
            // This simplified implementation takes the first floor's points
            // In practice, you'll need a proper polygon union algorithm
            mergedFloor.Points = floors[0].Points;

            return mergedFloor;
        }
    }
}
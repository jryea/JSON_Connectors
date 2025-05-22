using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.ModelLayout;
using Revit.Export.Properties;
using Revit.Utilities;
using System.Diagnostics;

namespace Revit.Export
{
    // Builds complete structural model without filtering
    public class StructuralModelBuilder
    {
        private readonly ExportContext _context;
        private BaseModel _model;

        public StructuralModelBuilder(ExportContext context)
        {
            _context = context;
            _model = new BaseModel();
        }

        public BaseModel BuildModel()
        {
            Debug.WriteLine("StructuralModelBuilder: Starting model build");

            // 1. Initialize metadata
            InitializeMetadata();

            // 2. Build levels (including base level processing)
            BuildLevels();

            // 3. Build floor types
            BuildFloorTypes();

            // 4. Build ALL properties first (materials, then others)
            BuildProperties();

            // 5. Build ALL elements (no filtering)
            BuildElements();

            Debug.WriteLine("StructuralModelBuilder: Model build complete");
            return _model;
        }

        private void InitializeMetadata()
        {
            var projectInfo = new Core.Models.Metadata.ProjectInfo
            {
                ProjectName = _context.RevitDoc.ProjectInformation?.Name ?? _context.RevitDoc.Title,
                ProjectId = _context.RevitDoc.ProjectInformation?.Number ?? Guid.NewGuid().ToString(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };

            var units = new Core.Models.Metadata.Units
            {
                Length = "inches",
                Force = "pounds",
                Temperature = "fahrenheit"
            };

            var coordinates = Helpers.ExtractCoordinateSystem(_context.RevitDoc);

            _model.Metadata.ProjectInfo = projectInfo;
            _model.Metadata.Units = units;
            _model.Metadata.Coordinates = coordinates;
        }

        private void BuildLevels()
        {
            if (_context.CustomLevels != null && _context.CustomLevels.Count > 0)
            {
                // Use custom levels provided
                _model.ModelLayout.Levels = new List<Level>(_context.CustomLevels);
                Debug.WriteLine($"Using {_context.CustomLevels.Count} custom levels");
            }
            else
            {
                // Build levels from Revit
                _model.ModelLayout.Levels = new List<Level>();

                foreach (var kvp in _context.RevitLevels)
                {
                    var revitLevel = kvp.Value;
                    var modelLevelId = _context.GetModelLevelId(kvp.Key);

                    var level = new Level
                    {
                        Id = modelLevelId,
                        Name = revitLevel.Name,
                        Elevation = revitLevel.Elevation * 12.0 // Convert feet to inches
                    };

                    _model.ModelLayout.Levels.Add(level);
                }

                Debug.WriteLine($"Built {_model.ModelLayout.Levels.Count} levels from Revit");
            }

            // Process base level if specified
            ProcessBaseLevel();
            // Build grids (part of ModelLayout)
            if (_context.ShouldExportElement("Grids"))
            {
                _model.ModelLayout.Grids = new List<Core.Models.ModelLayout.Grid>();
                var gridExport = new ModelLayout.GridExport(_context.RevitDoc);
                int gridCount = gridExport.Export(_model.ModelLayout.Grids);
                Debug.WriteLine($"Built {gridCount} grids");
            }
        }

        private void ProcessBaseLevel()
        {
            if (_context.BaseLevel == null || _model.ModelLayout.Levels == null) return;

            // Find the base level in our model
            var baseModelLevel = _model.ModelLayout.Levels.FirstOrDefault(l =>
                l.Name == _context.BaseLevel.Name ||
                Math.Abs(l.Elevation - (_context.BaseLevel.Elevation * 12.0)) < 0.1);

            if (baseModelLevel == null) return;

            double originalElevation = baseModelLevel.Elevation;

            // Rename to "Base" and set elevation to 0
            baseModelLevel.Name = "Base";
            baseModelLevel.Elevation = 0.0;

            // Adjust all other levels relative to base
            if (Math.Abs(originalElevation) > 0.001)
            {
                foreach (var level in _model.ModelLayout.Levels)
                {
                    if (level != baseModelLevel)
                    {
                        level.Elevation -= originalElevation;
                    }
                }
            }

            Debug.WriteLine($"Processed base level: {_context.BaseLevel.Name} -> Base, adjusted {_model.ModelLayout.Levels.Count} levels");
        }

        private void BuildFloorTypes()
        {
            if (_context.CustomFloorTypes != null && _context.CustomFloorTypes.Count > 0)
            {
                // Use custom floor types
                _model.ModelLayout.FloorTypes = new List<FloorType>(_context.CustomFloorTypes);

                // Add Base floor type if we have a base level
                if (_context.BaseLevel != null)
                {
                    var baseFloorType = new FloorType("Base");
                    _model.ModelLayout.FloorTypes.Add(baseFloorType);

                    // Assign Base floor type to base level
                    var baseLevel = _model.ModelLayout.Levels.FirstOrDefault(l => l.Name == "Base");
                    if (baseLevel != null)
                    {
                        baseLevel.FloorTypeId = baseFloorType.Id;
                    }
                }

                Debug.WriteLine($"Using {_context.CustomFloorTypes.Count} custom floor types");
            }
            else
            {
                // Create floor types from levels
                _model.ModelLayout.FloorTypes = new List<FloorType>();

                foreach (var level in _model.ModelLayout.Levels)
                {
                    var floorType = new FloorType(level.Name);
                    _model.ModelLayout.FloorTypes.Add(floorType);
                    level.FloorTypeId = floorType.Id;
                }

                Debug.WriteLine($"Created {_model.ModelLayout.FloorTypes.Count} floor types from levels");
            }
        }

        private void BuildProperties()
        {
            Debug.WriteLine("Building properties...");

            // Initialize collections
            _model.Properties.Materials = new List<Core.Models.Properties.Material>();
            _model.Properties.WallProperties = new List<Core.Models.Properties.WallProperties>();
            _model.Properties.FloorProperties = new List<Core.Models.Properties.FloorProperties>();
            _model.Properties.FrameProperties = new List<Core.Models.Properties.FrameProperties>();

            // 1. Materials first (others reference these)
            var materialExport = new Properties.MaterialExport(_context.RevitDoc);
            int materialCount = materialExport.Export(_model.Properties.Materials, _context.MaterialFilters);
            Debug.WriteLine($"Built {materialCount} materials");

            // 2. Wall properties
            var wallPropsExport = new Properties.WallPropertiesExport(_context.RevitDoc);
            int wallPropsCount = wallPropsExport.Export(_model.Properties.WallProperties);
            Debug.WriteLine($"Built {wallPropsCount} wall properties");

            // 3. Floor properties
            var floorPropsExport = new Properties.FloorPropertiesExport(_context.RevitDoc);
            int floorPropsCount = floorPropsExport.Export(_model.Properties.FloorProperties);
            Debug.WriteLine($"Built {floorPropsCount} floor properties");

            // 4. Frame properties (depends on materials)
            var framePropsExport = new Properties.FramePropertiesExport(_context.RevitDoc);
            int framePropsCount = framePropsExport.Export(_model.Properties.FrameProperties, _model.Properties.Materials);
            Debug.WriteLine($"Built {framePropsCount} frame properties");
        }

        private void BuildElements()
        {
            Debug.WriteLine("Building elements...");

            // Initialize element collections
            _model.Elements.Walls = new List<Core.Models.Elements.Wall>();
            _model.Elements.Floors = new List<Core.Models.Elements.Floor>();
            _model.Elements.Columns = new List<Core.Models.Elements.Column>();
            _model.Elements.Beams = new List<Core.Models.Elements.Beam>();
            _model.Elements.Braces = new List<Core.Models.Elements.Brace>();
            _model.Elements.IsolatedFootings = new List<Core.Models.Elements.IsolatedFooting>();

            // Build grids
            if (_context.ShouldExportElement("Grids"))
            {
                var gridExport = new ModelLayout.GridExport(_context.RevitDoc);
                int gridCount = gridExport.Export(_model.ModelLayout.Grids);
                Debug.WriteLine($"Built {gridCount} grids");
            }

            // Build walls
            if (_context.ShouldExportElement("Walls"))
            {
                var wallExport = new Elements.WallExport(_context.RevitDoc);
                int wallCount = wallExport.Export(_model.Elements.Walls, _model);
                Debug.WriteLine($"Built {wallCount} walls");
            }

            // Build floors
            if (_context.ShouldExportElement("Floors"))
            {
                var floorExport = new Elements.FloorExport(_context.RevitDoc);
                int floorCount = floorExport.Export(_model.Elements.Floors, _model);
                Debug.WriteLine($"Built {floorCount} floors");
            }

            // Build columns
            if (_context.ShouldExportElement("Columns"))
            {
                var columnExport = new Elements.ColumnExport(_context.RevitDoc);
                int columnCount = columnExport.Export(_model.Elements.Columns, _model);
                Debug.WriteLine($"Built {columnCount} columns");
            }

            // Build beams
            if (_context.ShouldExportElement("Beams"))
            {
                var beamExport = new Elements.BeamExport(_context.RevitDoc);
                int beamCount = beamExport.Export(_model.Elements.Beams, _model);
                Debug.WriteLine($"Built {beamCount} beams");
            }

            // Build braces
            if (_context.ShouldExportElement("Braces"))
            {
                var braceExport = new Elements.BraceExport(_context.RevitDoc);
                int braceCount = braceExport.Export(_model.Elements.Braces, _model);
                Debug.WriteLine($"Built {braceCount} braces");
            }

            // Build footings
            if (_context.ShouldExportElement("Footings"))
            {
                var footingExport = new Elements.IsolatedFootingExport(_context.RevitDoc);
                int footingCount = footingExport.Export(_model.Elements.IsolatedFootings, _model);
                Debug.WriteLine($"Built {footingCount} footings");
            }
        }
    }
}
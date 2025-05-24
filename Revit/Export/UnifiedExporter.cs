using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Core.Converters;
using Core.Models;
using Core.Models.ModelLayout;
using Core.Models.Metadata;
using Core.Utilities;
using Revit.Export.Elements;
using Revit.Export.ModelLayout;
using Revit.Export.Properties;
using Revit.Utilities;

namespace Revit.Export
{
    /// <summary>
    /// Unified exporter that handles all export formats (ETABS, RAM, Grasshopper) 
    /// with a clean transformation pipeline and guaranteed debug file output
    /// </summary>
    public class UnifiedExporter
    {
        private readonly Document _document;
        private readonly Dictionary<string, bool> _elementFilters;
        private readonly Dictionary<string, bool> _materialFilters;

        public UnifiedExporter(Document document,
            Dictionary<string, bool> elementFilters = null,
            Dictionary<string, bool> materialFilters = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _elementFilters = elementFilters ?? GetDefaultElementFilters();
            _materialFilters = materialFilters ?? GetDefaultMaterialFilters();
        }

        /// <summary>
        /// Main export method - handles all formats uniformly with clear transformation pipeline
        /// </summary>
        public ExportResult Export(ExportOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var result = new ExportResult { Success = false };

            try
            {
                Debug.WriteLine("UnifiedExporter: Starting export");

                // Step 1: Create raw untransformed model
                var rawModel = CreateRawModel(options);
                result.ElementCount = CountElements(rawModel);

                // Step 2: Save pre-transform JSON
                if (options.SaveDebugFiles)
                {
                    var preTransformPath = Path.ChangeExtension(options.OutputPath, ".json");
                    JsonConverter.SaveToFile(rawModel, preTransformPath, false);
                    result.PreTransformJsonPath = preTransformPath;
                    Debug.WriteLine($"Saved pre-transform JSON: {preTransformPath}");
                }

                // Step 3: Apply all transformations
                var transformedModel = ApplyTransformations(rawModel, options);

                // Step 4: Save post-transform JSON (if transforms applied)
                if (options.SaveDebugFiles && options.HasTransformations())
                {
                    var postTransformPath = GetPostTransformPath(options.OutputPath);
                    JsonConverter.SaveToFile(transformedModel, postTransformPath, false);
                    result.PostTransformJsonPath = postTransformPath;
                    Debug.WriteLine($"Saved post-transform JSON: {postTransformPath}");
                }

                // Step 5: Export to target format
                ExportToTargetFormat(transformedModel, options);

                result.Success = true;
                result.OutputPath = options.OutputPath;
                Debug.WriteLine("UnifiedExporter: Export completed successfully");

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                Debug.WriteLine($"UnifiedExporter: Export failed - {ex.Message}");
                throw;
            }
        }

        private BaseModel CreateRawModel(ExportOptions options)
        {
            Debug.WriteLine("Creating raw model (no transformations)");

            var model = new BaseModel();

            // Initialize metadata
            InitializeMetadata(model);

            // Export model layout (levels, grids, floor types)
            ExportModelLayout(model, options);

            // Export properties (materials first, then others that depend on materials)
            ExportProperties(model);

            // Export structural elements
            ExportElements(model, options);

            Debug.WriteLine($"Raw model created with {CountElements(model)} total elements");
            return model;
        }

        private void InitializeMetadata(BaseModel model)
        {
            var projectInfo = new ProjectInfo
            {
                ProjectName = _document.ProjectInformation?.Name ?? _document.Title,
                ProjectId = _document.ProjectInformation?.Number ?? Guid.NewGuid().ToString(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };

            var units = new Units
            {
                Length = "inches",
                Force = "pounds",
                Temperature = "fahrenheit"
            };

            var coordinates = Helpers.ExtractCoordinateSystem(_document);

            model.Metadata.ProjectInfo = projectInfo;
            model.Metadata.Units = units;
            model.Metadata.Coordinates = coordinates;
        }

        private void ExportModelLayout(BaseModel model, ExportOptions options)
        {
            // Export levels (filtered if specified)
            model.ModelLayout.Levels = new List<Level>();
            var levelExport = new LevelExport(_document);
            var levelCount = levelExport.Export(model.ModelLayout.Levels, options.SelectedLevels);
            Debug.WriteLine($"Exported {levelCount} levels");

            // Export grids (if enabled)
            if (ShouldExportElement("Grids"))
            {
                model.ModelLayout.Grids = new List<Grid>();
                var gridExport = new GridExport(_document);
                var gridCount = gridExport.Export(model.ModelLayout.Grids);
                Debug.WriteLine($"Exported {gridCount} grids");
            }

            // Create default floor types from levels (will be replaced if custom ones provided)
            model.ModelLayout.FloorTypes = new List<FloorType>();
            foreach (var level in model.ModelLayout.Levels)
            {
                var floorType = new FloorType(level.Name);
                model.ModelLayout.FloorTypes.Add(floorType);
                level.FloorTypeId = floorType.Id;
            }
            Debug.WriteLine($"Created {model.ModelLayout.FloorTypes.Count} default floor types");
        }

        private void ExportProperties(BaseModel model)
        {
            // Export materials first (others depend on these)
            model.Properties.Materials = new List<Core.Models.Properties.Material>();
            var materialExport = new MaterialExport(_document);
            var materialCount = materialExport.Export(model.Properties.Materials, _materialFilters);
            Debug.WriteLine($"Exported {materialCount} materials");

            // Export wall properties
            model.Properties.WallProperties = new List<Core.Models.Properties.WallProperties>();
            var wallPropsExport = new WallPropertiesExport(_document);
            var wallPropsCount = wallPropsExport.Export(model.Properties.WallProperties);
            Debug.WriteLine($"Exported {wallPropsCount} wall properties");

            // Export floor properties
            model.Properties.FloorProperties = new List<Core.Models.Properties.FloorProperties>();
            var floorPropsExport = new FloorPropertiesExport(_document);
            var floorPropsCount = floorPropsExport.Export(model.Properties.FloorProperties);
            Debug.WriteLine($"Exported {floorPropsCount} floor properties");

            // Export frame properties (depends on materials)
            model.Properties.FrameProperties = new List<Core.Models.Properties.FrameProperties>();
            var framePropsExport = new FramePropertiesExport(_document);
            var framePropsCount = framePropsExport.Export(model.Properties.FrameProperties, model.Properties.Materials);
            Debug.WriteLine($"Exported {framePropsCount} frame properties");
        }

        private void ExportElements(BaseModel model, ExportOptions options)
        {
            // Initialize element collections
            model.Elements.Walls = new List<Core.Models.Elements.Wall>();
            model.Elements.Floors = new List<Core.Models.Elements.Floor>();
            model.Elements.Columns = new List<Core.Models.Elements.Column>();
            model.Elements.Beams = new List<Core.Models.Elements.Beam>();
            model.Elements.Braces = new List<Core.Models.Elements.Brace>();
            model.Elements.IsolatedFootings = new List<Core.Models.Elements.IsolatedFooting>();

            // Export each element type if enabled
            if (ShouldExportElement("Walls"))
            {
                var wallExport = new WallExport(_document);
                var wallCount = wallExport.Export(model.Elements.Walls, model);
                Debug.WriteLine($"Exported {wallCount} walls");
            }

            if (ShouldExportElement("Floors"))
            {
                var floorExport = new FloorExport(_document);
                var floorCount = floorExport.Export(model.Elements.Floors, model);
                Debug.WriteLine($"Exported {floorCount} floors");
            }

            if (ShouldExportElement("Columns"))
            {
                var columnExport = new ColumnExport(_document);
                var columnCount = columnExport.Export(model.Elements.Columns, model);
                Debug.WriteLine($"Exported {columnCount} columns");
            }

            if (ShouldExportElement("Beams"))
            {
                var beamExport = new BeamExport(_document);
                var beamCount = beamExport.Export(model.Elements.Beams, model);
                Debug.WriteLine($"Exported {beamCount} beams");
            }

            if (ShouldExportElement("Braces"))
            {
                var braceExport = new BraceExport(_document);
                var braceCount = braceExport.Export(model.Elements.Braces, model);
                Debug.WriteLine($"Exported {braceCount} braces");
            }

            if (ShouldExportElement("Footings"))
            {
                var footingExport = new IsolatedFootingExport(_document);
                var footingCount = footingExport.Export(model.Elements.IsolatedFootings, model);
                Debug.WriteLine($"Exported {footingCount} footings");
            }

            // Apply level filtering to elements
            FilterElementsByLevels(model, options);
        }

        private BaseModel ApplyTransformations(BaseModel model, ExportOptions options)
        {
            if (!options.HasTransformations())
            {
                Debug.WriteLine("No transformations to apply");
                return model;
            }

            Debug.WriteLine("Applying transformations");

            // Clone the model to avoid modifying the original
            var workingModel = CloneModel(model);

            // Apply base level transformation
            if (options.BaseLevel != null)
            {
                ApplyBaseLevelTransformation(workingModel, options.BaseLevel);
            }

            // Apply custom floor types/levels
            if (options.CustomFloorTypes != null && options.CustomFloorTypes.Count > 0)
            {
                ApplyCustomFloorTypes(workingModel, options.CustomFloorTypes, options.CustomLevels);
            }

            // Apply rotation
            if (Math.Abs(options.RotationAngle) > 0.001)
            {
                var center = CalculateModelCenter(workingModel);
                Core.Models.ModelTransformation.RotateModel(workingModel, options.RotationAngle, center);
                Debug.WriteLine($"Applied {options.RotationAngle}° rotation around center ({center.X:F2}, {center.Y:F2})");
            }

            return workingModel;
        }

        private void ExportToTargetFormat(BaseModel model, ExportOptions options)
        {
            switch (options.Format)
            {
                case ExportFormat.ETABS:
                    ExportToETABS(model, options.OutputPath);
                    break;
                case ExportFormat.RAM:
                    ExportToRAM(model, options.OutputPath);
                    break;
                case ExportFormat.Grasshopper:
                    ExportToGrasshopper(model, options);
                    break;
                default:
                    throw new ArgumentException($"Unsupported export format: {options.Format}");
            }
        }

        private void ExportToETABS(BaseModel model, string outputPath)
        {
            // Save model to temp JSON file
            var tempJsonPath = Path.GetTempFileName();
            JsonConverter.SaveToFile(model, tempJsonPath, false);

            try
            {
                // Convert to ETABS format
                string jsonContent = File.ReadAllText(tempJsonPath);
                var converter = new ETABS.ETABSImport();
                string e2kContent = converter.ProcessModel(jsonContent);
                File.WriteAllText(outputPath, e2kContent);
                Debug.WriteLine($"Exported to ETABS format: {outputPath}");
            }
            finally
            {
                if (File.Exists(tempJsonPath))
                    File.Delete(tempJsonPath);
            }
        }

        private void ExportToRAM(BaseModel model, string outputPath)
        {
            // Save model to temp JSON file
            var tempJsonPath = Path.GetTempFileName();
            JsonConverter.SaveToFile(model, tempJsonPath, false);

            try
            {
                // Convert to RAM format
                var ramImporter = new RAM.RAMImporter();
                var result = ramImporter.ConvertJSONFileToRAM(tempJsonPath, outputPath);

                if (!result.Success)
                    throw new Exception($"RAM conversion failed: {result.Message}");

                Debug.WriteLine($"Exported to RAM format: {outputPath}");
            }
            finally
            {
                if (File.Exists(tempJsonPath))
                    File.Delete(tempJsonPath);
            }
        }

        private void ExportToGrasshopper(BaseModel model, ExportOptions options)
        {
            // For Grasshopper, save the JSON directly
            JsonConverter.SaveToFile(model, options.OutputPath, false);
            Debug.WriteLine($"Exported to Grasshopper format: {options.OutputPath}");

            // Export CAD files if view mappings provided
            if (options.FloorTypeToViewMap != null && options.FloorTypeToViewMap.Count > 0)
            {
                var dwgFolder = Path.Combine(Path.GetDirectoryName(options.OutputPath), "CAD");
                ExportCADFiles(dwgFolder, options.FloorTypeToViewMap, model.ModelLayout.FloorTypes);
            }
        }

        private void ExportCADFiles(string dwgFolder, Dictionary<string, ElementId> floorTypeToViewMap, List<FloorType> floorTypes)
        {
            try
            {
                Directory.CreateDirectory(dwgFolder);

                var options = new DWGExportOptions();
                var exportedViews = new HashSet<ElementId>();

                foreach (var floorType in floorTypes)
                {
                    if (floorTypeToViewMap.TryGetValue(floorType.Id, out ElementId viewId) &&
                        !exportedViews.Contains(viewId))
                    {
                        var view = _document.GetElement(viewId) as View;
                        if (view != null)
                        {
                            string fileName = SanitizeFilename($"{floorType.Name}_Plan");
                            _document.Export(dwgFolder, fileName, new List<ElementId> { viewId }, options);
                            exportedViews.Add(viewId);
                            Debug.WriteLine($"Exported CAD view: {fileName}.dwg");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting CAD files: {ex.Message}");
                // Don't fail the entire export for CAD export issues
            }
        }

        #region Helper Methods

        private BaseModel CloneModel(BaseModel original)
        {
            // Simple clone via JSON serialization
            var json = JsonConverter.Serialize(original);
            return JsonConverter.Deserialize(json, false);
        }

        private void ApplyBaseLevelTransformation(BaseModel model, Level baseLevel)
        {
            var modelBaseLevel = model.ModelLayout.Levels.FirstOrDefault(l =>
                l.Name == baseLevel.Name ||
                Math.Abs(l.Elevation - (baseLevel.Elevation * 12.0)) < 0.1);

            if (modelBaseLevel == null) return;

            double originalElevation = modelBaseLevel.Elevation;

            // Rename to "Base" and set elevation to 0
            modelBaseLevel.Name = "Base";
            modelBaseLevel.Elevation = 0.0;

            // Adjust all other levels relative to base
            foreach (var level in model.ModelLayout.Levels)
            {
                if (level != modelBaseLevel)
                {
                    level.Elevation -= originalElevation;
                }
            }

            Debug.WriteLine($"Applied base level transformation: {baseLevel.Name} -> Base");
        }

        private void ApplyCustomFloorTypes(BaseModel model, List<FloorType> customFloorTypes, List<Level> customLevels)
        {
            if (customFloorTypes != null)
            {
                model.ModelLayout.FloorTypes = new List<FloorType>(customFloorTypes);
            }

            if (customLevels != null)
            {
                model.ModelLayout.Levels = new List<Level>(customLevels);
            }

            Debug.WriteLine($"Applied custom floor types: {customFloorTypes?.Count ?? 0}, custom levels: {customLevels?.Count ?? 0}");
        }

        private void FilterElementsByLevels(BaseModel model, ExportOptions options)
        {
            if (options.SelectedLevels == null || options.SelectedLevels.Count == 0)
                return;

            // Get model level IDs that correspond to selected Revit levels
            var selectedModelLevelIds = new HashSet<string>();
            var revitLevels = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToDictionary(l => l.Id, l => l);

            foreach (var revitLevelId in options.SelectedLevels)
            {
                if (revitLevels.TryGetValue(revitLevelId, out Level revitLevel))
                {
                    var modelLevel = model.ModelLayout.Levels.FirstOrDefault(l =>
                        l.Name == revitLevel.Name ||
                        Math.Abs(l.Elevation - (revitLevel.Elevation * 12.0)) < 0.1);

                    if (modelLevel != null)
                        selectedModelLevelIds.Add(modelLevel.Id);
                }
            }

            // Filter levels
            model.ModelLayout.Levels = model.ModelLayout.Levels
                .Where(l => selectedModelLevelIds.Contains(l.Id))
                .ToList();

            // Filter elements by level
            FilterElementsByLevelIds(model, selectedModelLevelIds);

            Debug.WriteLine($"Filtered to {selectedModelLevelIds.Count} selected levels");
        }

        private void FilterElementsByLevelIds(BaseModel model, HashSet<string> selectedLevelIds)
        {
            model.Elements.Walls = model.Elements.Walls?.Where(w =>
                selectedLevelIds.Contains(w.TopLevelId ?? "")).ToList();

            model.Elements.Floors = model.Elements.Floors?.Where(f =>
                selectedLevelIds.Contains(f.LevelId ?? "")).ToList();

            model.Elements.Columns = model.Elements.Columns?.Where(c =>
                selectedLevelIds.Contains(c.TopLevelId ?? "")).ToList();

            model.Elements.Beams = model.Elements.Beams?.Where(b =>
                selectedLevelIds.Contains(b.LevelId ?? "")).ToList();

            model.Elements.Braces = model.Elements.Braces?.Where(br =>
                selectedLevelIds.Contains(br.TopLevelId ?? "")).ToList();

            model.Elements.IsolatedFootings = model.Elements.IsolatedFootings?.Where(f =>
                selectedLevelIds.Contains(f.LevelId ?? "")).ToList();
        }

        private Core.Models.Geometry.Point2D CalculateModelCenter(BaseModel model)
        {
            var allPoints = new List<Core.Models.Geometry.Point2D>();

            // Collect points from grids
            if (model.ModelLayout?.Grids != null)
            {
                foreach (var grid in model.ModelLayout.Grids)
                {
                    if (grid.StartPoint != null) allPoints.Add(new Core.Models.Geometry.Point2D(grid.StartPoint.X, grid.StartPoint.Y));
                    if (grid.EndPoint != null) allPoints.Add(new Core.Models.Geometry.Point2D(grid.EndPoint.X, grid.EndPoint.Y));
                }
            }

            // Collect points from elements
            if (model.Elements != null)
            {
                if (model.Elements.Beams != null)
                    foreach (var beam in model.Elements.Beams)
                    {
                        if (beam.StartPoint != null) allPoints.Add(beam.StartPoint);
                        if (beam.EndPoint != null) allPoints.Add(beam.EndPoint);
                    }

                if (model.Elements.Columns != null)
                    foreach (var column in model.Elements.Columns)
                    {
                        if (column.StartPoint != null) allPoints.Add(column.StartPoint);
                    }
            }

            // Return geometric center or origin if no points found
            if (allPoints.Count == 0)
                return new Core.Models.Geometry.Point2D(0, 0);

            double centerX = allPoints.Average(p => p.X);
            double centerY = allPoints.Average(p => p.Y);

            return new Core.Models.Geometry.Point2D(centerX, centerY);
        }

        private int CountElements(BaseModel model)
        {
            int count = 0;
            count += model.ModelLayout?.Levels?.Count ?? 0;
            count += model.ModelLayout?.Grids?.Count ?? 0;
            count += model.Properties?.Materials?.Count ?? 0;
            count += model.Elements?.Walls?.Count ?? 0;
            count += model.Elements?.Floors?.Count ?? 0;
            count += model.Elements?.Columns?.Count ?? 0;
            count += model.Elements?.Beams?.Count ?? 0;
            count += model.Elements?.Braces?.Count ?? 0;
            count += model.Elements?.IsolatedFootings?.Count ?? 0;
            return count;
        }

        private bool ShouldExportElement(string elementType)
        {
            return _elementFilters.ContainsKey(elementType) && _elementFilters[elementType];
        }

        private string GetPostTransformPath(string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputPath);
            return Path.Combine(directory, fileNameWithoutExtension + "-transformed.json");
        }

        private string SanitizeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return "unnamed";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = string.Join("_", filename.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            sanitized = sanitized.Replace(" ", "_").Replace(".", "_").Replace(",", "_").Replace(":", "_");

            if (sanitized.Length > 100)
                sanitized = sanitized.Substring(0, 100);

            return sanitized.Trim('_');
        }

        private Dictionary<string, bool> GetDefaultElementFilters()
        {
            return new Dictionary<string, bool>
            {
                { "Grids", true },
                { "Beams", true },
                { "Braces", true },
                { "Columns", true },
                { "Floors", true },
                { "Walls", true },
                { "Footings", true }
            };
        }

        private Dictionary<string, bool> GetDefaultMaterialFilters()
        {
            return new Dictionary<string, bool>
            {
                { "Steel", true },
                { "Concrete", true }
            };
        }

        #endregion
    }
}
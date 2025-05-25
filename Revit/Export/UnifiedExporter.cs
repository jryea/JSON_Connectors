using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Core.Converters;
using Core.Models;
using Core.Utilities;
using Revit.Export.Elements;
using Revit.Export.ModelLayout;
using Revit.Export.Properties;
using Revit.Utilities;

// Consistent aliases matching existing codebase pattern
using CL = Core.Models.ModelLayout;
using CM = Core.Models.Metadata;

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

        public ExportResult Export(ExportOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var result = new ExportResult { Success = false };

            try
            {
                Debug.WriteLine("UnifiedExporter: Starting clean export");

                // Step 1: Create complete model with transformations applied
                var model = CreateCompleteModel(options);
                result.ElementCount = CountElements(model);

                // Step 2: Create ONE JSON file (no duplicates)
                var jsonPath = DetermineJsonPath(options);
                JsonConverter.SaveToFile(model, jsonPath, removeDuplicates: true);
                result.PreTransformJsonPath = jsonPath;

                // Step 3: Convert to target format if needed (clean handoff to external converters)
                if (options.Format != ExportFormat.Grasshopper)
                {
                    ConvertToTargetFormat(jsonPath, options.OutputPath, options.Format);
                }

                // Step 4: Handle CAD export for Grasshopper if needed
                if (options.Format == ExportFormat.Grasshopper && options.FloorTypeToViewMap != null)
                {
                    ExportCADFiles(options);
                }

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

        /// <summary>
        /// Creates complete model with all transformations applied (no intermediate files)
        /// </summary>
        private BaseModel CreateCompleteModel(ExportOptions options)
        {
            Debug.WriteLine("Creating complete model with transformations");

            var model = new BaseModel();

            // Initialize metadata
            InitializeMetadata(model);

            // Export model layout (levels, grids, floor types)
            ExportModelLayout(model, options);

            // Export elements with level filtering applied
            ExportFilteredElements(model, options);

            // Export only properties that are used by remaining elements
            ExportUsedProperties(model);

            // Apply all transformations in memory
            ApplyTransformations(model, options);

            Debug.WriteLine($"Complete model created with {CountElements(model)} elements");
            return model;
        }

        // Export properties AFTER elements are filtered - only export what's actually used
        private void ExportUsedProperties(BaseModel model)
        {
            // Step 1: Collect property IDs used by elements
            var usedPropertyIds = CollectUsedPropertyIds(model);

            // Step 2: Export ALL properties first
            model.Properties.Materials = new List<Core.Models.Properties.Material>();
            model.Properties.WallProperties = new List<Core.Models.Properties.WallProperties>();
            model.Properties.FloorProperties = new List<Core.Models.Properties.FloorProperties>();
            model.Properties.FrameProperties = new List<Core.Models.Properties.FrameProperties>();

            // Export materials first (others depend on these) - FIXED method call
            var materialExport = new MaterialExport(_document);
            var materialCount = materialExport.Export(model.Properties.Materials, _materialFilters);
            Debug.WriteLine($"Exported {materialCount} materials");

            // Export wall properties
            var wallPropsExport = new WallPropertiesExport(_document);
            var wallPropsCount = wallPropsExport.Export(model.Properties.WallProperties);
            Debug.WriteLine($"Exported {wallPropsCount} wall properties");

            // Export floor properties
            var floorPropsExport = new FloorPropertiesExport(_document);
            var floorPropsCount = floorPropsExport.Export(model.Properties.FloorProperties);
            Debug.WriteLine($"Exported {floorPropsCount} floor properties");

            // Export frame properties last (depends on materials) - FIXED method call
            var framePropsExport = new FramePropertiesExport(_document);
            var framePropsCount = framePropsExport.Export(model.Properties.FrameProperties, model.Properties.Materials);
            Debug.WriteLine($"Exported {framePropsCount} frame properties");

            // Step 3: Now collect ALL used property IDs (including indirect material references)
            var allUsedPropertyIds = CollectAllUsedPropertyIds(model);

            // Step 4: Filter properties to only those that are actually used
            FilterToUsedProperties(model, allUsedPropertyIds);
        }

        /// <summary>
        /// Filters the model's properties to only include those that are actually used by elements
        /// </summary>
        private void FilterToUsedProperties(BaseModel model, UsedPropertyIds usedPropertyIds)
        {
            if (model.Properties == null || usedPropertyIds == null) return;

            // Filter materials
            if (model.Properties.Materials != null)
            {
                model.Properties.Materials = model.Properties.Materials
                    .Where(m => usedPropertyIds.MaterialIds.Contains(m.Id))
                    .ToList();
                Debug.WriteLine($"Filtered to {model.Properties.Materials.Count} used materials");
            }

            // Filter wall properties
            if (model.Properties.WallProperties != null)
            {
                model.Properties.WallProperties = model.Properties.WallProperties
                    .Where(wp => usedPropertyIds.WallPropertyIds.Contains(wp.Id))
                    .ToList();
                Debug.WriteLine($"Filtered to {model.Properties.WallProperties.Count} used wall properties");
            }

            // Filter floor properties
            if (model.Properties.FloorProperties != null)
            {
                model.Properties.FloorProperties = model.Properties.FloorProperties
                    .Where(fp => usedPropertyIds.FloorPropertyIds.Contains(fp.Id))
                    .ToList();
                Debug.WriteLine($"Filtered to {model.Properties.FloorProperties.Count} used floor properties");
            }

            // Filter frame properties
            if (model.Properties.FrameProperties != null)
            {
                model.Properties.FrameProperties = model.Properties.FrameProperties
                    .Where(fp => usedPropertyIds.FramePropertyIds.Contains(fp.Id))
                    .ToList();
                Debug.WriteLine($"Filtered to {model.Properties.FrameProperties.Count} used frame properties");
            }

            // Filter diaphragms
            if (model.Properties.Diaphragms != null)
            {
                model.Properties.Diaphragms = model.Properties.Diaphragms
                    .Where(d => usedPropertyIds.DiaphragmIds.Contains(d.Id))
                    .ToList();
                Debug.WriteLine($"Filtered to {model.Properties.Diaphragms.Count} used diaphragms");
            }
        }

        /// <summary>
        /// Exports structural elements with level filtering applied
        /// </summary>
        private void ExportFilteredElements(BaseModel model, ExportOptions options)
        {
            // First export all elements
            ExportElements(model, options);

            // Then apply level filtering if specified
            if (options.SelectedLevels != null && options.SelectedLevels.Count > 0)
            {
                FilterElementsByLevels(model, options);
            }

            Debug.WriteLine($"Exported and filtered elements. Total count: {CountElements(model)}");
        }

        /// <summary>
        /// Collects all property IDs that are actually used by elements in the model
        /// </summary>
        private UsedPropertyIds CollectUsedPropertyIds(BaseModel model)
        {
            var usedIds = new UsedPropertyIds();

            if (model.Elements == null) return usedIds;

            // Collect from walls
            if (model.Elements.Walls != null)
            {
                foreach (var wall in model.Elements.Walls)
                {
                    if (!string.IsNullOrEmpty(wall.PropertiesId))
                        usedIds.WallPropertyIds.Add(wall.PropertiesId);
                }
            }

            // Collect from floors
            if (model.Elements.Floors != null)
            {
                foreach (var floor in model.Elements.Floors)
                {
                    if (!string.IsNullOrEmpty(floor.FloorPropertiesId))
                        usedIds.FloorPropertyIds.Add(floor.FloorPropertiesId);
                    if (!string.IsNullOrEmpty(floor.DiaphragmId))
                        usedIds.DiaphragmIds.Add(floor.DiaphragmId);
                }
            }

            // Collect from columns
            if (model.Elements.Columns != null)
            {
                foreach (var column in model.Elements.Columns)
                {
                    if (!string.IsNullOrEmpty(column.FramePropertiesId))
                        usedIds.FramePropertyIds.Add(column.FramePropertiesId);
                }
            }

            // Collect from beams
            if (model.Elements.Beams != null)
            {
                foreach (var beam in model.Elements.Beams)
                {
                    if (!string.IsNullOrEmpty(beam.FramePropertiesId))
                        usedIds.FramePropertyIds.Add(beam.FramePropertiesId);
                }
            }

            // Collect from braces
            if (model.Elements.Braces != null)
            {
                foreach (var brace in model.Elements.Braces)
                {
                    if (!string.IsNullOrEmpty(brace.FramePropertiesId))
                        usedIds.FramePropertyIds.Add(brace.FramePropertiesId);
                    if (!string.IsNullOrEmpty(brace.MaterialId))
                        usedIds.MaterialIds.Add(brace.MaterialId);
                }
            }

            // Collect from isolated footings
            if (model.Elements.IsolatedFootings != null)
            {
                foreach (var footing in model.Elements.IsolatedFootings)
                {
                    if (!string.IsNullOrEmpty(footing.MaterialId))
                        usedIds.MaterialIds.Add(footing.MaterialId);
                }
            }

            // Now collect indirect material references from properties
            CollectIndirectMaterialReferences(model, usedIds);

            Debug.WriteLine($"Collected used property IDs - Materials: {usedIds.MaterialIds.Count}, " +
                           $"WallProps: {usedIds.WallPropertyIds.Count}, " +
                           $"FloorProps: {usedIds.FloorPropertyIds.Count}, " +
                           $"FrameProps: {usedIds.FramePropertyIds.Count}, " +
                           $"Diaphragms: {usedIds.DiaphragmIds.Count}");

            return usedIds;
        }

        /// <summary>
        /// Determine single JSON file path - no duplicates
        /// </summary>
        private string DetermineJsonPath(ExportOptions options)
        {
            if (options.Format == ExportFormat.Grasshopper)
            {
                // For Grasshopper, JSON IS the final format
                return options.OutputPath;
            }
            else
            {
                // For ETABS/RAM, create temporary JSON for conversion
                return Path.ChangeExtension(options.OutputPath, ".json");
            }
        }

        /// <summary>
        /// Convert to target format using EXTERNAL converters (clean handoff)
        /// NO ETABS/RAM logic in this project
        /// </summary>
        private void ConvertToTargetFormat(string jsonPath, string outputPath, ExportFormat format)
        {
            string jsonContent = File.ReadAllText(jsonPath);

            try
            {
                switch (format)
                {
                    case ExportFormat.ETABS:
                        // Clean handoff to ETABS project
                        var etabsConverter = new ETABS.ETABSImport();
                        string e2kContent = etabsConverter.ProcessModel(jsonContent);
                        File.WriteAllText(outputPath, e2kContent);
                        Debug.WriteLine($"Converted to ETABS: {outputPath}");
                        break;

                    case ExportFormat.RAM:
                        // Clean handoff to RAM project
                        var ramConverter = new RAM.RAMImporter();
                        var result = ramConverter.ConvertJSONStringToRAM(jsonContent, outputPath);
                        if (!result.Success)
                            throw new Exception($"RAM conversion failed: {result.Message}");
                        Debug.WriteLine($"Converted to RAM: {outputPath}");
                        break;

                    default:
                        throw new ArgumentException($"Unsupported format for conversion: {format}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Format conversion failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Export CAD files for Grasshopper (minimal logic, no format conversion)
        /// </summary>
        private void ExportCADFiles(ExportOptions options)
        {
            try
            {
                if (options.FloorTypeToViewMap == null || options.FloorTypeToViewMap.Count == 0)
                    return;

                var dwgFolder = Path.Combine(Path.GetDirectoryName(options.OutputPath), "CAD");
                Directory.CreateDirectory(dwgFolder);

                var dwgOptions = new DWGExportOptions();
                var exportedViews = new HashSet<ElementId>();

                // Simple CAD export - no complex logic
                foreach (var mapping in options.FloorTypeToViewMap)
                {
                    if (!exportedViews.Contains(mapping.Value))
                    {
                        var view = _document.GetElement(mapping.Value) as View;
                        if (view != null)
                        {
                            string fileName = SanitizeFilename($"FloorPlan_{view.Name}");
                            _document.Export(dwgFolder, fileName, new List<ElementId> { mapping.Value }, dwgOptions);
                            exportedViews.Add(mapping.Value);
                            Debug.WriteLine($"Exported CAD view: {fileName}.dwg");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CAD export failed (non-critical): {ex.Message}");
                // Don't fail the entire export for CAD issues
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

            // Export structural elements FIRST
            ExportElements(model, options);

            // Apply level filtering to elements BEFORE exporting properties
            FilterElementsByLevels(model, options);

            // Export properties AFTER filtering - only export properties that are actually used
            ExportPropertiesForFilteredElements(model);

            Debug.WriteLine($"Raw model created with {CountElements(model)} total elements");
            return model;
        }

        private void InitializeMetadata(BaseModel model)
        {
            var projectInfo = new CM.ProjectInfo
            {
                ProjectName = _document.ProjectInformation?.Name ?? _document.Title,
                ProjectId = _document.ProjectInformation?.Number ?? Guid.NewGuid().ToString(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };

            var units = new CM.Units
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
            model.ModelLayout.Levels = new List<CL.Level>();
            var levelExport = new LevelExport(_document);
            var levelCount = levelExport.Export(model.ModelLayout.Levels, options.SelectedLevels);
            Debug.WriteLine($"Exported {levelCount} levels");

            // Export grids (if enabled)
            if (ShouldExportElement("Grids"))
            {
                model.ModelLayout.Grids = new List<CL.Grid>();
                var gridExport = new GridExport(_document);
                var gridCount = gridExport.Export(model.ModelLayout.Grids);
                Debug.WriteLine($"Exported {gridCount} grids");
            }

            // Create default floor types from levels (will be replaced if custom ones provided)
            model.ModelLayout.FloorTypes = new List<CL.FloorType>();
            foreach (var level in model.ModelLayout.Levels)
            {
                var floorType = new CL.FloorType(level.Name);
                model.ModelLayout.FloorTypes.Add(floorType);
                level.FloorTypeId = floorType.Id;
            }
            Debug.WriteLine($"Created {model.ModelLayout.FloorTypes.Count} default floor types");
        }

        // NEW METHOD: Export only properties that are referenced by remaining elements
        // Fixed method for UnifiedExporter class
        private void ExportPropertiesForFilteredElements(BaseModel model)
        {
            // Step 1: Collect property IDs used directly by elements (not from properties yet)
            var usedPropertyIds = CollectDirectPropertyUsage(model);

            // Step 2: Export all properties first
            // Export materials first (others depend on these)
            model.Properties.Materials = new List<Core.Models.Properties.Material>();
            var materialExport = new MaterialExport(_document);
            var materialCount = materialExport.Export(model.Properties.Materials, _materialFilters);
            Debug.WriteLine($"Exported {materialCount} total materials");

            // Export wall properties
            model.Properties.WallProperties = new List<Core.Models.Properties.WallProperties>();
            var wallPropsExport = new WallPropertiesExport(_document);
            var wallPropsCount = wallPropsExport.Export(model.Properties.WallProperties);
            Debug.WriteLine($"Exported {wallPropsCount} total wall properties");

            // Export floor properties
            model.Properties.FloorProperties = new List<Core.Models.Properties.FloorProperties>();
            var floorPropsExport = new FloorPropertiesExport(_document);
            var floorPropsCount = floorPropsExport.Export(model.Properties.FloorProperties);
            Debug.WriteLine($"Exported {floorPropsCount} total floor properties");

            // Export frame properties last (depends on materials)
            model.Properties.FrameProperties = new List<Core.Models.Properties.FrameProperties>();
            var framePropsExport = new FramePropertiesExport(_document);
            var framePropsCount = framePropsExport.Export(model.Properties.FrameProperties, model.Properties.Materials);
            Debug.WriteLine($"Exported {framePropsCount} total frame properties");

            // Step 3: Now collect ALL used property IDs (including indirect material references from properties)
            var allUsedPropertyIds = CollectAllUsedPropertyIds(model);

            // Step 4: Filter properties to only those that are actually used
            model.Properties.Materials = model.Properties.Materials
                .Where(m => allUsedPropertyIds.MaterialIds.Contains(m.Id))
                .ToList();
            Debug.WriteLine($"Filtered to {model.Properties.Materials.Count} used materials");

            model.Properties.WallProperties = model.Properties.WallProperties
                .Where(wp => allUsedPropertyIds.WallPropertyIds.Contains(wp.Id))
                .ToList();
            Debug.WriteLine($"Filtered to {model.Properties.WallProperties.Count} used wall properties");

            model.Properties.FloorProperties = model.Properties.FloorProperties
                .Where(fp => allUsedPropertyIds.FloorPropertyIds.Contains(fp.Id))
                .ToList();
            Debug.WriteLine($"Filtered to {model.Properties.FloorProperties.Count} used floor properties");

            model.Properties.FrameProperties = model.Properties.FrameProperties
                .Where(fp => allUsedPropertyIds.FramePropertyIds.Contains(fp.Id))
                .ToList();
            Debug.WriteLine($"Filtered to {model.Properties.FrameProperties.Count} used frame properties");
        }

        // New method: Collect property IDs used directly by elements only
        private UsedPropertyIds CollectDirectPropertyUsage(BaseModel model)
        {
            var usedIds = new UsedPropertyIds();

            // Collect from beams
            if (model.Elements.Beams != null)
            {
                foreach (var beam in model.Elements.Beams)
                {
                    if (!string.IsNullOrEmpty(beam.FramePropertiesId))
                        usedIds.FramePropertyIds.Add(beam.FramePropertiesId);
                }
            }

            // Collect from columns
            if (model.Elements.Columns != null)
            {
                foreach (var column in model.Elements.Columns)
                {
                    if (!string.IsNullOrEmpty(column.FramePropertiesId))
                        usedIds.FramePropertyIds.Add(column.FramePropertiesId);
                }
            }

            // Collect from braces
            if (model.Elements.Braces != null)
            {
                foreach (var brace in model.Elements.Braces)
                {
                    if (!string.IsNullOrEmpty(brace.FramePropertiesId))
                        usedIds.FramePropertyIds.Add(brace.FramePropertiesId);
                    if (!string.IsNullOrEmpty(brace.MaterialId))
                        usedIds.MaterialIds.Add(brace.MaterialId);
                }
            }

            // Collect from walls
            if (model.Elements.Walls != null)
            {
                foreach (var wall in model.Elements.Walls)
                {
                    if (!string.IsNullOrEmpty(wall.PropertiesId))
                        usedIds.WallPropertyIds.Add(wall.PropertiesId);
                }
            }

            // Collect from floors
            if (model.Elements.Floors != null)
            {
                foreach (var floor in model.Elements.Floors)
                {
                    if (!string.IsNullOrEmpty(floor.FloorPropertiesId))
                        usedIds.FloorPropertyIds.Add(floor.FloorPropertiesId);
                    if (!string.IsNullOrEmpty(floor.DiaphragmId))
                        usedIds.DiaphragmIds.Add(floor.DiaphragmId);
                }
            }

            // Collect from isolated footings
            if (model.Elements.IsolatedFootings != null)
            {
                foreach (var footing in model.Elements.IsolatedFootings)
                {
                    if (!string.IsNullOrEmpty(footing.MaterialId))
                        usedIds.MaterialIds.Add(footing.MaterialId);
                }
            }

            Debug.WriteLine($"Collected direct property usage: Materials={usedIds.MaterialIds.Count}, " +
                           $"Frame={usedIds.FramePropertyIds.Count}, Wall={usedIds.WallPropertyIds.Count}, " +
                           $"Floor={usedIds.FloorPropertyIds.Count}");

            return usedIds;
        }

        // New method: Collect ALL used property IDs including indirect references from exported properties
        private UsedPropertyIds CollectAllUsedPropertyIds(BaseModel model)
        {
            // Start with direct usage from elements
            var usedIds = CollectDirectPropertyUsage(model);

            // Now add indirect material references from exported properties
            CollectIndirectMaterialReferences(model, usedIds);

            Debug.WriteLine($"Collected all used property IDs: Materials={usedIds.MaterialIds.Count}, " +
                           $"Frame={usedIds.FramePropertyIds.Count}, Wall={usedIds.WallPropertyIds.Count}, " +
                           $"Floor={usedIds.FloorPropertyIds.Count}");

            return usedIds;
        }

        // Helper class to collect used property IDs
        public class UsedPropertyIds
        {
            public HashSet<string> MaterialIds { get; set; } = new HashSet<string>();
            public HashSet<string> FramePropertyIds { get; set; } = new HashSet<string>();
            public HashSet<string> WallPropertyIds { get; set; } = new HashSet<string>();
            public HashSet<string> FloorPropertyIds { get; set; } = new HashSet<string>();
            public HashSet<string> DiaphragmIds { get; set; } = new HashSet<string>();
        }

        // Collect material IDs that are referenced by properties (not just elements)
        private void CollectIndirectMaterialReferences(BaseModel model, UsedPropertyIds usedIds)
        {
            // Add materials referenced by wall properties
            if (model.Properties?.WallProperties != null)
            {
                foreach (var wallProp in model.Properties.WallProperties)
                {
                    if (!string.IsNullOrEmpty(wallProp.MaterialId))
                        usedIds.MaterialIds.Add(wallProp.MaterialId);
                }
            }

            // Add materials referenced by floor properties  
            if (model.Properties?.FloorProperties != null)
            {
                foreach (var floorProp in model.Properties.FloorProperties)
                {
                    if (!string.IsNullOrEmpty(floorProp.MaterialId))
                        usedIds.MaterialIds.Add(floorProp.MaterialId);
                }
            }

            // Add materials referenced by frame properties
            if (model.Properties?.FrameProperties != null)
            {
                foreach (var frameProp in model.Properties.FrameProperties)
                {
                    if (!string.IsNullOrEmpty(frameProp.MaterialId))
                        usedIds.MaterialIds.Add(frameProp.MaterialId);
                }
            }
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

            // Note: Level filtering moved to CreateRawModel() before properties are exported
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

        private void ExportCADFiles(string dwgFolder, Dictionary<string, ElementId> floorTypeToViewMap, List<CL.FloorType> floorTypes)
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

        private void ApplyBaseLevelTransformation(BaseModel model, Autodesk.Revit.DB.Level baseLevel)
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

        private void ApplyCustomFloorTypes(BaseModel model, List<CL.FloorType> customFloorTypes, List<CL.Level> customLevels)
        {
            if (customFloorTypes != null)
            {
                model.ModelLayout.FloorTypes = new List<CL.FloorType>(customFloorTypes);
            }

            if (customLevels != null)
            {
                model.ModelLayout.Levels = new List<CL.Level>(customLevels);
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
                .OfClass(typeof(Autodesk.Revit.DB.Level))
                .Cast<Autodesk.Revit.DB.Level>()
                .ToDictionary(l => l.Id, l => l);

            foreach (var revitLevelId in options.SelectedLevels)
            {
                if (revitLevels.TryGetValue(revitLevelId, out Autodesk.Revit.DB.Level revitLevel))
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
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using CM = Core.Models.Metadata;
using CL = Core.Models.ModelLayout;
using Revit.Export.Models;
using Revit.Export.ModelLayout;
using Revit.Export.Elements;
using Revit.Export.Properties;
using Revit.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Revit.Export
{
    /// <summary>
    /// Exports Revit models to Grasshopper format, with support for floor type associations
    /// </summary>
    public class GrasshopperExporter
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;
        private BaseModel _model;

        public GrasshopperExporter(Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
            _model = new BaseModel();
        }

        /// <summary>
        /// Exports the entire model to Grasshopper format
        /// </summary>
        /// <param name="jsonPath">Path to save the JSON file</param>
        /// <param name="dwgFolder">Path to save CAD exports</param>
        public void ExportAll(string jsonPath, string dwgFolder)
        {
            try
            {
                // Initialize a fresh model
                _model = new BaseModel();

                // Initialize metadata
                InitializeMetadata();

                // Export model structure
                ExportModelStructure();

                // Export CAD plans
                ExportCADPlans(dwgFolder);

                // Save the model to JSON
                JsonConverter.SaveToFile(_model, jsonPath);

                Debug.WriteLine($"Successfully exported complete model to {jsonPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting complete model: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Exports selected sheets with proper floor type associations
        /// </summary>
        /// <param name="jsonPath">Path to save the JSON file</param>
        /// <param name="dwgFolder">Path to save CAD exports</param>
        /// <param name="selectedSheets">List of sheets to export</param>
        /// <param name="floorTypes">List of floor types to include</param>
        /// <param name="referencePoint">Reference point for the model</param>
        /// <param name="levelToFloorTypeMap">Mapping between level IDs and floor type IDs</param>
        public void ExportSelectedSheets(
            string jsonPath,
            string dwgFolder,
            List<SheetViewModel> selectedSheets,
            List<CL.FloorType> floorTypes,
            XYZ referencePoint,
            Dictionary<string, string> levelToFloorTypeMap = null)
        {
            try
            {
                // Re-initialize the model
                _model = new BaseModel();

                // Initialize metadata with reference point
                InitializeMetadata(referencePoint);

                // Add the floor types to the model
                _model.ModelLayout.FloorTypes = floorTypes;

                // Export structural elements and layout
                ExportModelStructure();

                // Now that levels are exported, associate them with floor types
                AssociateLevelsWithFloorTypes(levelToFloorTypeMap);

                // Export selected CAD plans
                ExportSelectedCADPlans(dwgFolder, selectedSheets);

                // Save the model to JSON
                JsonConverter.SaveToFile(_model, jsonPath);

                Debug.WriteLine($"Successfully exported model to {jsonPath} with {_model.ModelLayout.Levels.Count} levels and {_model.ModelLayout.FloorTypes.Count} floor types");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting model: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Associates levels with floor types based on the provided mapping
        /// </summary>
        private void AssociateLevelsWithFloorTypes(Dictionary<string, string> levelToFloorTypeMap)
        {
            if (levelToFloorTypeMap == null || levelToFloorTypeMap.Count == 0 ||
                _model.ModelLayout.Levels.Count == 0 || _model.ModelLayout.FloorTypes.Count == 0)
            {
                // Use default floor type for all levels if no mapping is provided
                AssociateWithDefaultFloorType();
                return;
            }

            // Get default floor type to use as fallback
            string defaultFloorTypeId = _model.ModelLayout.FloorTypes.FirstOrDefault()?.Id;

            // Create a dictionary to track Revit level IDs to model level IDs
            var revitToModelLevelMap = new Dictionary<string, string>();

            // We'll be tracking mapping by level name since the IDs are different between Revit and the model
            foreach (var level in _model.ModelLayout.Levels)
            {
                // For each level in the model, find its corresponding level in Revit by name
                var revitLevels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .Where(l => l.Name == level.Name)
                    .ToList();

                // Associate this level with a floor type
                if (revitLevels.Any())
                {
                    var revitLevel = revitLevels.First();
                    // Try to find the floor type ID for this level
                    if (levelToFloorTypeMap.TryGetValue(revitLevel.Id.ToString(), out string floorTypeId))
                    {
                        // Verify the floor type exists
                        if (_model.ModelLayout.FloorTypes.Any(ft => ft.Id == floorTypeId))
                        {
                            level.FloorTypeId = floorTypeId;
                            Debug.WriteLine($"Associated level '{level.Name}' with floor type '{floorTypeId}'");
                        }
                        else
                        {
                            level.FloorTypeId = defaultFloorTypeId;
                            Debug.WriteLine($"Level '{level.Name}' mapped to unknown floor type - using default");
                        }
                    }
                    else
                    {
                        level.FloorTypeId = defaultFloorTypeId;
                        Debug.WriteLine($"Level '{level.Name}' not in mapping - using default floor type");
                    }
                }
                else
                {
                    level.FloorTypeId = defaultFloorTypeId;
                    Debug.WriteLine($"Could not find Revit level for '{level.Name}' - using default floor type");
                }
            }
        }

        /// <summary>
        /// Associates all levels with the default floor type
        /// </summary>
        private void AssociateWithDefaultFloorType()
        {
            if (_model.ModelLayout.Levels.Count == 0 || _model.ModelLayout.FloorTypes.Count == 0)
                return;

            // Get the first floor type as default
            string defaultFloorTypeId = _model.ModelLayout.FloorTypes.FirstOrDefault()?.Id;
            if (defaultFloorTypeId == null)
                return;

            // Set all levels to use the default floor type
            foreach (var level in _model.ModelLayout.Levels)
            {
                level.FloorTypeId = defaultFloorTypeId;
            }

            Debug.WriteLine($"Associated all levels with default floor type '{defaultFloorTypeId}'");
        }

        /// <summary>
        /// Initializes the model metadata
        /// </summary>
        private void InitializeMetadata(XYZ referencePoint = null)
        {
            // Initialize project info
            CM.ProjectInfo projectInfo = new CM.ProjectInfo
            {
                ProjectName = _doc.ProjectInformation?.Name ?? _doc.Title,
                ProjectId = _doc.ProjectInformation?.Number ?? Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };

            // Initialize units
            CM.Units units = new CM.Units
            {
                Length = "inches",
                Force = "pounds",
                Temperature = "fahrenheit"
            };

            // Set the metadata
            _model.Metadata.ProjectInfo = projectInfo;
            _model.Metadata.Units = units;

            // Extract coordinates using the Helpers class
            _model.Metadata.Coordinates = Helpers.ExtractCoordinateSystem(_doc);

            // Add reference point if provided
            if (referencePoint != null)
            {
                // Add reference point to coordinates
                if (_model.Metadata.Coordinates == null)
                {
                    _model.Metadata.Coordinates = new CM.Coordinates();
                }

                // Convert to point in model format (feet to inches)
                _model.Metadata.Coordinates.ProjectBasePoint = new Core.Models.Geometry.Point3D(
                    referencePoint.X * 12.0,
                    referencePoint.Y * 12.0,
                    referencePoint.Z * 12.0
                );

                Debug.WriteLine($"Set reference point: X={referencePoint.X}, Y={referencePoint.Y}, Z={referencePoint.Z}");
            }
        }

        /// <summary>
        /// Exports layout elements and structural elements
        /// </summary>
        private void ExportModelStructure()
        {
            // Export layout elements first (levels, grids)
            ExportLayoutElements();

            // Export structural elements (walls, floors, columns, etc.)
            ExportStructuralElements();
        }

        /// <summary>
        /// Exports layout elements from Revit to the model
        /// </summary>
        private void ExportLayoutElements()
        {
            try
            {
                // Export levels first
                LevelExport levelExport = new LevelExport(_doc);
                int levelCount = levelExport.Export(_model.ModelLayout.Levels);
                Debug.WriteLine($"Exported {levelCount} levels");

                // Then, after levels are exported, export floor types
                // This helps ensure that types will be correctly associated with levels later
                FloorTypeExport floorTypeExport = new FloorTypeExport(_doc);
                int floorTypeCount = floorTypeExport.Export(_model.ModelLayout.FloorTypes, _model.ModelLayout.Levels);
                Debug.WriteLine($"Exported {floorTypeCount} floor types");

                // Export grids
                GridExport gridExport = new GridExport(_doc);
                int gridCount = gridExport.Export(_model.ModelLayout.Grids);
                Debug.WriteLine($"Exported {gridCount} grids");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting layout elements: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Exports structural elements from Revit to the model
        /// </summary>
        private void ExportStructuralElements()
        {
            try
            {
                // Export materials first 
                MaterialExport materialExport = new MaterialExport(_doc);
                int materialCount = materialExport.Export(_model.Properties.Materials);
                Debug.WriteLine($"Exported {materialCount} materials");

                // Export properties that reference materials
                WallPropertiesExport wallPropertiesExport = new WallPropertiesExport(_doc);
                int wallPropsCount = wallPropertiesExport.Export(_model.Properties.WallProperties, _model.Properties.Materials);
                Debug.WriteLine($"Exported {wallPropsCount} wall properties");

                FloorPropertiesExport floorPropertiesExport = new FloorPropertiesExport(_doc);
                int floorPropsCount = floorPropertiesExport.Export(_model.Properties.FloorProperties);
                Debug.WriteLine($"Exported {floorPropsCount} floor properties");

                FramePropertiesExport framePropertiesExport = new FramePropertiesExport(_doc);
                int framePropsCount = framePropertiesExport.Export(_model.Properties.FrameProperties, _model.Properties.Materials);
                Debug.WriteLine($"Exported {framePropsCount} frame properties");

                // Export structural elements
                FloorExport floorExport = new FloorExport(_doc);
                int floorCount = floorExport.Export(_model.Elements.Floors, _model);
                Debug.WriteLine($"Exported {floorCount} floors");

                ColumnExport columnExport = new ColumnExport(_doc);
                int columnCount = columnExport.Export(_model.Elements.Columns, _model);
                Debug.WriteLine($"Exported {columnCount} columns");

                WallExport wallExport = new WallExport(_doc);
                int wallCount = wallExport.Export(_model.Elements.Walls, _model);
                Debug.WriteLine($"Exported {wallCount} walls");

                BeamExport beamExport = new BeamExport(_doc);
                int beamCount = beamExport.Export(_model.Elements.Beams, _model);
                Debug.WriteLine($"Exported {beamCount} beams");

                BraceExport braceExport = new BraceExport(_doc);
                int braceCount = braceExport.Export(_model.Elements.Braces, _model);
                Debug.WriteLine($"Exported {braceCount} braces");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting structural elements: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Exports all CAD plans
        /// </summary>
        private void ExportCADPlans(string folderPath)
        {
            try
            {
                // Use CADExporter class to export all plans
                CADExporter exporter = new CADExporter(_doc);
                exporter.ExportCADPlans(folderPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting CAD plans: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports selected CAD plans
        /// </summary>
        private void ExportSelectedCADPlans(string folderPath, List<SheetViewModel> selectedSheets)
        {
            try
            {
                // Create folder if it doesn't exist
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Set up export options
                DWGExportOptions options = new DWGExportOptions();

                // Track view IDs that were exported
                HashSet<ElementId> exportedViewIds = new HashSet<ElementId>();

                // Export each selected view to DWG
                foreach (var sheetView in selectedSheets)
                {
                    if (!sheetView.IsSelected || sheetView.ViewId == null)
                        continue;

                    // Skip if already exported this view
                    if (exportedViewIds.Contains(sheetView.ViewId))
                        continue;

                    // Get the view from the document
                    View view = _doc.GetElement(sheetView.ViewId) as View;
                    if (view == null)
                        continue;

                    // Create filename based on sheet number and view name
                    string baseFilename = SanitizeFilename($"{sheetView.SheetNumber}_{sheetView.SheetName}_{view.Name}");

                    try
                    {
                        // Export to DWG using Revit API
                        _doc.Export(folderPath, baseFilename, new List<ElementId> { view.Id }, options);

                        // Add to tracked views
                        exportedViewIds.Add(sheetView.ViewId);

                        Debug.WriteLine($"Exported view {view.Name} to {baseFilename}.dwg");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error exporting view {view.Name}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"Exported {exportedViewIds.Count} views to {folderPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting selected CAD plans: {ex.Message}");
            }
        }

        public void ExportWithFloorTypeViewMappings(
    string jsonPath,
    string dwgFolder,
    List<CL.FloorType> floorTypes,
    XYZ referencePoint,
    Dictionary<string, string> levelToFloorTypeMap,
    Dictionary<string, ElementId> floorTypeToViewMap)
        {
            try
            {
                // Re-initialize the model
                _model = new BaseModel();

                // Initialize metadata with reference point
                InitializeMetadata(referencePoint);

                // Add the floor types to the model
                _model.ModelLayout.FloorTypes = floorTypes;

                // Export structural elements
                ExportModelStructure();

                // Apply level to floor type mappings
                ApplyLevelToFloorTypeMappings(levelToFloorTypeMap);

                // Export CAD plans based on floor type to view mappings
                ExportFloorTypeViews(dwgFolder, floorTypeToViewMap);

                // Save the model to JSON
                JsonConverter.SaveToFile(_model, jsonPath);

                Debug.WriteLine($"Successfully exported model to {jsonPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting model: {ex.Message}");
                throw;
            }
        }

        private void ApplyLevelToFloorTypeMappings(Dictionary<string, string> levelToFloorTypeMap)
        {
            // Skip if no mappings
            if (levelToFloorTypeMap == null || levelToFloorTypeMap.Count == 0)
                return;

            // Update each level with its associated floor type
            foreach (var level in _model.ModelLayout.Levels)
            {
                // Find if we have a mapping for this level
                foreach (var mapping in levelToFloorTypeMap)
                {
                    // Check if the ID formats match (Revit Element ID vs. internal ID)
                    if (level.Id.EndsWith(mapping.Key) || mapping.Key.EndsWith(level.Id))
                    {
                        level.FloorTypeId = mapping.Value;
                        Debug.WriteLine($"Mapped level {level.Name} to floor type {mapping.Value}");
                        break;
                    }
                }
            }
        }

        private void ExportFloorTypeViews(string folderPath, Dictionary<string, ElementId> floorTypeToViewMap)
        {
            try
            {
                // Create folder if it doesn't exist
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Skip if no mappings
                if (floorTypeToViewMap == null || floorTypeToViewMap.Count == 0)
                    return;

                // Set up export options
                DWGExportOptions options = new DWGExportOptions();

                // Track exported views to avoid duplicates
                HashSet<ElementId> exportedViewIds = new HashSet<ElementId>();

                // For each floor type in the model, export its associated view
                foreach (var floorType in _model.ModelLayout.FloorTypes)
                {
                    // Check if we have a view mapping for this floor type
                    if (floorTypeToViewMap.TryGetValue(floorType.Id, out ElementId viewId))
                    {
                        // Skip if already exported
                        if (exportedViewIds.Contains(viewId))
                            continue;

                        // Get the view from the document
                        View view = _doc.GetElement(viewId) as View;
                        if (view == null)
                            continue;

                        // Create filename based on floor type name
                        string baseFilename = SanitizeFilename($"{floorType.Name}_Plan");

                        try
                        {
                            // Export to DWG using Revit API
                            _doc.Export(folderPath, baseFilename, new List<ElementId> { viewId }, options);

                            // Add to tracked views
                            exportedViewIds.Add(viewId);

                            Debug.WriteLine($"Exported view {view.Name} to {baseFilename}.dwg for floor type {floorType.Name}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error exporting view {view.Name}: {ex.Message}");
                        }
                    }
                }

                Debug.WriteLine($"Exported {exportedViewIds.Count} views to {folderPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting floor type views: {ex.Message}");
            }
        }

        // Sanitizes a filename by removing invalid characters
        private string SanitizeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return "unnamed";

            // Remove invalid file system characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = string.Join("_", filename.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            // Replace common problematic characters
            sanitized = sanitized.Replace(" ", "_")
                                .Replace(".", "_")
                                .Replace(",", "_")
                                .Replace(":", "_");

            // Limit length
            if (sanitized.Length > 100)
                sanitized = sanitized.Substring(0, 100);

            return sanitized.Trim('_');
        }
    }
}
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

        public void ExportAll(string jsonPath, string dwgFolder)
        {
            // Initialize metadata
            InitializeMetadata();

            // Export model structure
            ExportModelStructure();

            // Export CAD plans
            ExportCADPlans(dwgFolder);

            // Save the model to JSON
            JsonConverter.SaveToFile(_model, jsonPath);
        }

        public void ExportSelectedSheets(string jsonPath, string dwgFolder, List<SheetViewModel> selectedSheets,
                                        List<CL.FloorType> floorTypes, XYZ referencePoint)
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

                // Export selected CAD plans
                ExportSelectedCADPlans(dwgFolder, selectedSheets);

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

        private void InitializeMetadata(XYZ referencePoint = null)
        {
            // Initialize project info
            CM.ProjectInfo projectInfo = new CM.ProjectInfo
            {
                ProjectName = _doc.ProjectInformation?.Name ?? _doc.Title,
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

        private void ExportModelStructure()
        {
            // Export layout elements (levels, grids)
            ExportLayoutElements();

            // Export structural elements (walls, floors, columns, etc.)
            ExportStructuralElements();
        }

        private void ExportLayoutElements()
        {
            try
            {
                // Export floor types
                FloorTypeExport floorTypeExport = new FloorTypeExport(_doc);
                int floorTypeCount = floorTypeExport.Export(_model.ModelLayout.FloorTypes);
                Debug.WriteLine($"Exported {floorTypeCount} floor types");

                // Export levels
                LevelExport levelExport = new LevelExport(_doc);
                int levelCount = levelExport.Export(_model.ModelLayout.Levels);
                Debug.WriteLine($"Exported {levelCount} levels");

                // Export grids
                GridExport gridExport = new GridExport(_doc);
                int gridCount = gridExport.Export(_model.ModelLayout.Grids);
                Debug.WriteLine($"Exported {gridCount} grids");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting layout elements: {ex.Message}");
            }
        }

        private void ExportStructuralElements()
        {
            try
            {
                // Export floors
                FloorExport floorExport = new FloorExport(_doc);
                int floorCount = floorExport.Export(_model.Elements.Floors, _model);
                Debug.WriteLine($"Exported {floorCount} floors");

                // Export columns
                ColumnExport columnExport = new ColumnExport(_doc);
                int columnCount = columnExport.Export(_model.Elements.Columns, _model);
                Debug.WriteLine($"Exported {columnCount} columns");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting structural elements: {ex.Message}");
            }
        }

        private void ExportCADPlans(string folderPath)
        {
            // Use CADExporter class to export all plans
            CADExporter exporter = new CADExporter(_doc);
            exporter.ExportCADPlans(folderPath);
        }

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
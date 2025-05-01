using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models.Geometry;
using Core.Models.Metadata;
using Core.Models;
using Core.Models.ModelLayout;
using Core.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Revit.Export.Models;
using Revit.Export.Properties;

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
            // Initialize metadata including project coordinates
            InitializeMetadata();

            // Export elements reusing your existing export functionality
            ExportModelStructure();

            // Export CAD plans
            ExportCADPlans(dwgFolder);

            // Save the model to JSON
            JsonConverter.SaveToFile(_model, jsonPath);
        }

        public void ExportSelectedSheets(string jsonPath, string dwgFolder, List<SheetViewModel> selectedSheets,
                                      List<FloorType> floorTypes, XYZ referencePoint)
        {
            try
            {
                // Re-initialize the model
                _model = new BaseModel();

                // Initialize metadata with reference point
                InitializeMetadataWithReferencePoint(referencePoint);

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

        private void InitializeMetadata()
        {
            // Initialize project info
            ProjectInfo projectInfo = new ProjectInfo
            {
                ProjectName = _doc.ProjectInformation?.Name ?? _doc.Title,
                ProjectId = _doc.ProjectInformation?.Number ?? Guid.NewGuid().ToString(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };

            // Initialize units
            Units units = new Units
            {
                Length = "inches",
                Force = "pounds",
                Temperature = "fahrenheit"
            };

            // Set the metadata
            _model.Metadata.ProjectInfo = projectInfo;
            _model.Metadata.Units = units;

            // Extract and set coordinate system
            _model.Metadata.Coordinates = ExtractCoordinateSystem();
        }

        private void InitializeMetadataWithReferencePoint(XYZ referencePoint)
        {
            // Standard metadata initialization
            InitializeMetadata();

            // Additional setup for reference point
            if (referencePoint != null)
            {
                // Add reference point to coordinates
                if (_model.Metadata.Coordinates == null)
                {
                    _model.Metadata.Coordinates = new Coordinates();
                }

                // Convert to points in model format
                _model.Metadata.Coordinates.ProjectBasePoint = new Point3D(
                    referencePoint.X * 12.0, // Convert from feet to inches
                    referencePoint.Y * 12.0,
                    referencePoint.Z * 12.0
                );

                Debug.WriteLine($"Set reference point: X={referencePoint.X}, Y={referencePoint.Y}, Z={referencePoint.Z}");
            }
        }

        private Coordinates ExtractCoordinateSystem()
        {
            Coordinates coords = new Coordinates();

            try
            {
                // Get Project Base Point
                BasePoint projectBasePoint = GetProjectBasePoint();
                if (projectBasePoint != null)
                {
                    coords.ProjectBasePoint = new Point3D(
                        projectBasePoint.Position.X * 12.0, // Convert to inches
                        projectBasePoint.Position.Y * 12.0,
                        projectBasePoint.Position.Z * 12.0
                    );

                    // Get angle to true north
                    coords.Rotation = projectBasePoint.GetProjectRotation();

                    Debug.WriteLine($"Extracted project base point: X={projectBasePoint.Position.X}, " +
                                    $"Y={projectBasePoint.Position.Y}, Z={projectBasePoint.Position.Z}");
                }

                // Get Survey Point if available
                BasePoint surveyPoint = GetSurveyPoint();
                if (surveyPoint != null)
                {
                    coords.SurveyPoint = new Point3D(
                        surveyPoint.Position.X * 12.0,
                        surveyPoint.Position.Y * 12.0,
                        surveyPoint.Position.Z * 12.0
                    );

                    Debug.WriteLine($"Extracted survey point: X={surveyPoint.Position.X}, " +
                                   $"Y={surveyPoint.Position.Y}, Z={surveyPoint.Position.Z}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting coordinate system: {ex.Message}");
            }

            return coords;
        }

        private BasePoint GetProjectBasePoint()
        {
            try
            {
                // Find the project base point in the document
                FilteredElementCollector collector = new FilteredElementCollector(_doc);
                collector.OfClass(typeof(BasePoint));

                // Get all base points
                var basePoints = collector.Cast<BasePoint>().ToList();

                // Find the project base point
                return basePoints.FirstOrDefault(bp => bp.IsProjectBasePoint);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting project base point: {ex.Message}");
                return null;
            }
        }

        private BasePoint GetSurveyPoint()
        {
            try
            {
                // Find the survey point in the document
                FilteredElementCollector collector = new FilteredElementCollector(_doc);
                collector.OfClass(typeof(BasePoint));

                // Get all base points
                var basePoints = collector.Cast<BasePoint>().ToList();

                // Find the survey point
                return basePoints.FirstOrDefault(bp => bp.IsSurveyPoint);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting survey point: {ex.Message}");
                return null;
            }
        }

        private void ExportModelStructure()
        {
            // Export layout elements
            ExportLayoutElements();

            // Export properties
            ExportProperties();

            // Export structural elements
            ExportStructuralElements();
        }

        private void ExportLayoutElements()
        {
            try
            {
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

        private void ExportProperties()
        {
            try
            {
                // Export materials
                MaterialExport materialExport = new MaterialExport(_doc);
                int materialCount = materialExport.Export(_model.Properties.Materials);
                Debug.WriteLine($"Exported {materialCount} materials");

                // Export other property definitions
                WallPropertiesExport wallPropertiesExport = new WallPropertiesExport(_doc);
                int wallPropsCount = wallPropertiesExport.Export(_model.Properties.WallProperties, _model.Properties.Materials);
                Debug.WriteLine($"Exported {wallPropsCount} wall properties");

                FloorPropertiesExport floorPropertiesExport = new FloorPropertiesExport(_doc);
                int floorPropsCount = floorPropertiesExport.Export(_model.Properties.FloorProperties);
                Debug.WriteLine($"Exported {floorPropsCount} floor properties");

                FramePropertiesExport framePropertiesExport = new FramePropertiesExport(_doc);
                int framePropsCount = framePropertiesExport.Export(_model.Properties.FrameProperties, _model.Properties.Materials);
                Debug.WriteLine($"Exported {framePropsCount} frame properties");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting properties: {ex.Message}");
            }
        }

        private void ExportStructuralElements()
        {
            try
            {
                // Export walls
                WallExport wallExport = new WallExport(_doc);
                int wallCount = wallExport.Export(_model.Elements.Walls, _model);
                Debug.WriteLine($"Exported {wallCount} walls");

                // Export floors
                FloorExport floorExport = new FloorExport(_doc);
                int floorCount = floorExport.Export(_model.Elements.Floors, _model);
                Debug.WriteLine($"Exported {floorCount} floors");

                // Export columns
                ColumnExport columnExport = new ColumnExport(_doc);
                int columnCount = columnExport.Export(_model.Elements.Columns, _model);
                Debug.WriteLine($"Exported {columnCount} columns");

                // Export beams
                BeamExport beamExport = new BeamExport(_doc);
                int beamCount = beamExport.Export(_model.Elements.Beams, _model);
                Debug.WriteLine($"Exported {beamCount} beams");

                // Export braces
                BraceExport braceExport = new BraceExport(_doc);
                int braceCount = braceExport.Export(_model.Elements.Braces, _model);
                Debug.WriteLine($"Exported {braceCount} braces");

                // Export spread footings
                IsolatedFootingExport isolatedFootingExport = new IsolatedFootingExport(_doc);
                int footingCount = isolatedFootingExport.Export(_model.Elements.IsolatedFootings, _model);
                Debug.WriteLine($"Exported {footingCount} isolated footings");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting structural elements: {ex.Message}");
            }
        }


    }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting CAD plans: {ex.Message}");
            }
        }
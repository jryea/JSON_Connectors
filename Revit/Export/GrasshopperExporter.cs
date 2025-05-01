using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models.Geometry;
using Core.Models.Metadata;
using Core.Models;
using Core.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private void InitializeMetadata()
        {
            // Set metadata like in your existing exporter
            // Plus add coordinate system info
            _model.Metadata.Coordinates = ExtractCoordinateSystem();
        }

        private Coordinates ExtractCoordinateSystem()
        {
            Coordinates coords = new Coordinates();

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
            }

            // Similar code for Survey Point

            return coords;
        }

        private void ExportCADPlans(string folderPath)
        {
            // Get all floor plan views
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .WhereElementIsNotElementType()
                .Cast<View>()
                .Where(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate)
                .ToList();

            // Setup DWG export options
            DWGExportOptions options = new DWGExportOptions
            {
                MergedViews = false,
                ExportingLinks = true,
                FileVersion = ACADVersion.R2018
            };

            // Export each view to DWG
            foreach (View view in views)
            {
                string filename = Path.Combine(folderPath,
                    SanitizeFilename(view.Name) + ".dwg");

                try
                {
                    _doc.Export(folderPath, view.Name + ".dwg",
                        new List<ElementId> { view.Id }, options);

                    Debug.WriteLine($"Exported view {view.Name} to {filename}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting view {view.Name}: {ex.Message}");
                }
            }
        }

        private void ExportModelStructure()
        {
            // This could reuse your existing export methods
            // For example:
            ExportLayoutElements();
            ExportProperties();
            ExportStructuralElements();

            // Additional method for consolidating floors by level
            ConsolidateFloorsByLevel();
        }

        private void ConsolidateFloorsByLevel()
        {
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
            // Simplified implementation - in practice you'd do proper polygon union

            // Use the first floor as a template
            Floor mergedFloor = new Floor
            {
                LevelId = floors[0].LevelId,
                FloorPropertiesId = floors[0].FloorPropertiesId,
                DiaphragmId = floors[0].DiaphragmId,
                Points = new List<Point2D>()
            };

            // Union all floor perimeters 
            // (Simplified - real implementation would use proper Boolean operations)

            // For the demo, just take the first floor's points
            mergedFloor.Points = floors[0].Points;

            return mergedFloor;
        }
    }
}

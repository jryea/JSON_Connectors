using Rhino;
using Rhino.Commands;
using Rhino.UI;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.FileIO;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Converters;

namespace StructuralSetup.Commands
{
    [System.Runtime.InteropServices.Guid("82e1f6b0-c1d9-4cb3-b48f-6d2ac6f8a456")]
    public class ParametricSetupCommand : Command
    {
        public ParametricSetupCommand()
        {
            Instance = this;
        }

        public static ParametricSetupCommand Instance { get; private set; }
        public override string EnglishName => "ParametricSetup";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Step 1: Prompt for JSON file selection
            string jsonFilePath = SelectJsonFile();
            if (string.IsNullOrEmpty(jsonFilePath))
                return Result.Cancel;

            // Step 2: Deserialize the JSON file
            BaseModel model;
            try
            {
                model = JsonConverter.LoadFromFile(jsonFilePath);
                if (model == null)
                {
                    RhinoApp.WriteLine("Failed to load model from JSON file.");
                    return Result.Failure;
                }
                RhinoApp.WriteLine($"Successfully loaded model: {model.Metadata?.ProjectInfo?.ProjectName ?? "Unnamed Project"}");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error deserializing JSON: {ex.Message}");
                return Result.Failure;
            }
            // Step 3: Prompt for point selection
            GetPoint gp = new GetPoint();
            gp.SetCommandPrompt("Select origin point for model import (or press Enter to use origin)");
            gp.AcceptNothing(true);

            // Get the result without casting
            var getResult = gp.Get();

            // Convert GetResult to Result appropriately
            if (getResult == GetResult.Cancel)
                return Result.Cancel;

            Point3d originPoint = (getResult == GetResult.Point) ? gp.Point() : Point3d.Origin;

            // Debug output
            RhinoApp.WriteLine($"DEBUG - User point selection: {getResult}, Point: X={originPoint.X}, Y={originPoint.Y}, Z={originPoint.Z}");

            // Step 4: Begin setup
            RhinoApp.RunScript("_-Command _Pause", false);

            try
            {
                // Create layer structure based on floor types in the model
                var layerStructure = CreateLayerStructure(doc, model);

                // Import grids from the model
                ImportGrids(doc, model, layerStructure, originPoint);

                // Import floors from the model
                ImportFloors(doc, model, layerStructure, originPoint);

                // Optional: Import CAD reference if available
                string projectDirectory = Path.GetDirectoryName(jsonFilePath);
                string cadFolder = Path.Combine(projectDirectory, "CAD");
                if (Directory.Exists(cadFolder))
                {
                    string dwgFile = Directory.GetFiles(cadFolder, "*.dwg").FirstOrDefault();
                    if (!string.IsNullOrEmpty(dwgFile))
                    {
                        bool importCad = true;
                        string fileName = Path.GetFileName(dwgFile);
                        Result cadResult = RhinoGet.GetBool($"Import CAD file: {fileName}?", true, "No", "Yes", ref importCad);

                        if (cadResult == Result.Success && importCad)
                        {
                            ImportDwgFile(doc, dwgFile, layerStructure);
                        }
                    }
                }

                RhinoApp.RunScript("_-Command _Resume", false);
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("Error during setup: {0}", ex.Message);
                RhinoApp.RunScript("_-Command _Cancel", false);
                return Result.Failure;
            }
        }

        private string SelectJsonFile()
        {
            using (var dialog = new Eto.Forms.OpenFileDialog
            {
                Title = "Select JSON Model File",
                Filters = { new Eto.Forms.FileFilter("JSON Files", "*.json") }
            })
            {
                var result = dialog.ShowDialog(RhinoEtoApp.MainWindow);
                if (result == Eto.Forms.DialogResult.Ok)
                    return dialog.FileName;
            }
            return null;
        }

        private LayerStructure CreateLayerStructure(RhinoDoc doc, BaseModel model)
        {
            // Create the main setup layer
            int setupLayerIndex = GetOrCreateLayer(doc, "z-setup");

            // Create backgrounds layer
            int backgroundsLayerIndex = GetOrCreateLayer(doc, "z-backgrounds", setupLayerIndex);

            // Create grids layer
            int gridsLayerIndex = GetOrCreateLayer(doc, "z-grids", setupLayerIndex);

            var layers = new LayerStructure
            {
                SetupLayerIndex = setupLayerIndex,
                BackgroundsLayerIndex = backgroundsLayerIndex,
                GridsLayerIndex = gridsLayerIndex,
                FloorplateLayers = new Dictionary<string, int>(),
                FloorTypeLayers = new Dictionary<string, int>(),
                LevelElevations = new Dictionary<string, double>()
            };

            // Store level elevations
            if (model.ModelLayout?.Levels != null)
            {
                foreach (var level in model.ModelLayout.Levels)
                {
                    // Store level elevations in feet (convert from inches if needed)
                    layers.LevelElevations[level.Id] = level.Elevation / 12.0;
                    RhinoApp.WriteLine($"Level {level.Name}: {layers.LevelElevations[level.Id]} ft");
                }
            }

            // Create floorplate layers based on model's floor types
            if (model.ModelLayout?.FloorTypes != null)
            {
                int floorplateCounter = 1;
                foreach (var floorType in model.ModelLayout.FloorTypes)
                {
                    string floorplateName = $"z-floorplate{floorplateCounter}";
                    int floorplateIndex = GetOrCreateLayer(doc, floorplateName, setupLayerIndex);

                    // Store both mappings for reference
                    layers.FloorplateLayers[floorType.Id] = floorplateIndex;
                    layers.FloorTypeLayers[floorType.Id] = floorplateIndex;

                    floorplateCounter++;
                }
            }

            return layers;
        }

        private void ImportGrids(RhinoDoc doc, BaseModel model, LayerStructure layers, Point3d originPoint)
        {
            if (model?.ModelLayout?.Grids == null || model.ModelLayout.Grids.Count == 0)
            {
                RhinoApp.WriteLine("No grids found in the model.");
                return;
            }

            RhinoApp.WriteLine($"Importing {model.ModelLayout.Grids.Count} grids...");

            foreach (var grid in model.ModelLayout.Grids)
            {
                if (grid.StartPoint == null || grid.EndPoint == null)
                {
                    RhinoApp.WriteLine("Skipping grid with invalid points");
                    continue;
                }

                // Convert from inches to feet and place at Z=0
                // Use originPoint as actual 0,0 point in the model
                Point3d startPoint = new Point3d(
                    originPoint.X + (grid.StartPoint.X / 12.0),
                    originPoint.Y + (grid.StartPoint.Y / 12.0),
                    originPoint.Z);  // Keep Z coordinate from origin point

                Point3d endPoint = new Point3d(
                    originPoint.X + (grid.EndPoint.X / 12.0),
                    originPoint.Y + (grid.EndPoint.Y / 12.0),
                    originPoint.Z);  // Keep Z coordinate from origin point

                // Create a line from the start and end points
                Line gridLine = new Line(startPoint, endPoint);

                // Add the grid line to the document
                var attr = new ObjectAttributes();
                attr.LayerIndex = layers.GridsLayerIndex;
                attr.Name = grid.Name ?? $"Grid-{grid.Id}";

                doc.Objects.AddLine(gridLine, attr);
            }
        }

        private void ImportFloors(RhinoDoc doc, BaseModel model, LayerStructure layers, Point3d originPoint)
        {
            if (model?.Elements?.Floors == null || model.Elements.Floors.Count == 0)
            {
                RhinoApp.WriteLine("No floors found in the model.");
                return;
            }

            RhinoApp.WriteLine($"Importing {model.Elements.Floors.Count} floors...");

            foreach (var floor in model.Elements.Floors)
            {
                // Skip if no level ID or not enough points
                if (string.IsNullOrEmpty(floor.LevelId) || floor.Points == null || floor.Points.Count < 3)
                {
                    RhinoApp.WriteLine("Skipping invalid floor (no level or insufficient points)");
                    continue;
                }

                // Get elevation for this floor's level
                if (!layers.LevelElevations.TryGetValue(floor.LevelId, out double elevation))
                {
                    RhinoApp.WriteLine($"Skipping floor with unknown level ID: {floor.LevelId}");
                    continue;
                }

                // Find the right layer for this floor based on level's floor type
                Level level = model.ModelLayout?.Levels?.FirstOrDefault(l => l.Id == floor.LevelId);
                if (level == null || string.IsNullOrEmpty(level.FloorTypeId))
                {
                    RhinoApp.WriteLine($"Skipping floor with missing floor type");
                    continue;
                }

                // Set layer index based on floor type
                int layerIndex;
                if (!layers.FloorTypeLayers.TryGetValue(level.FloorTypeId, out layerIndex))
                {
                    RhinoApp.WriteLine($"No layer found for floor type: {level.FloorTypeId}, using setup layer");
                    layerIndex = layers.SetupLayerIndex;
                }

                // Create polyline from points
                var polylinePoints = new List<Point3d>();
                foreach (var point in floor.Points)
                {
                    // Convert from inches to feet and offset by origin point
                    polylinePoints.Add(new Point3d(
                        originPoint.X + (point.X / 12.0),
                        originPoint.Y + (point.Y / 12.0),
                        originPoint.Z + elevation));
                }

                // Close the polyline if not already closed
                if (polylinePoints.Count > 2 &&
                    (polylinePoints[0] != polylinePoints[polylinePoints.Count - 1]))
                {
                    polylinePoints.Add(polylinePoints[0]);
                }

                var polyline = new Polyline(polylinePoints);
                var curve = polyline.ToNurbsCurve();

                // Add the curve to the document
                var attr = new ObjectAttributes();
                attr.LayerIndex = layerIndex;
                attr.Name = $"Floor-{floor.Id}";
                doc.Objects.AddCurve(curve, attr);
            }
        }

        private bool ImportDwgFile(RhinoDoc doc, string filePath, LayerStructure layers)
        {
            try
            {
                // Set the current layer to z-backgrounds
                doc.Layers.SetCurrentLayerIndex(layers.BackgroundsLayerIndex, true);

                var dwgOptions = new FileDwgReadOptions();
                dwgOptions.LayoutUnits = UnitSystem.Inches;
                dwgOptions.ImportUnreferencedLayers = true;

                bool success = FileDwg.Read(filePath, doc, dwgOptions);
                return success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Import error: {ex.Message}");
                return false;
            }
        }

        private int GetOrCreateLayer(RhinoDoc doc, string layerName, int parentIndex = -1)
        {
            // Check if layer already exists
            int layerIndex = doc.Layers.Find(layerName, true);

            if (layerIndex >= 0)
            {
                // If found, make sure it has the correct parent
                if (parentIndex >= 0)
                {
                    var layer = doc.Layers[layerIndex];
                    if (layer.ParentLayerId != doc.Layers[parentIndex].Id)
                    {
                        layer.ParentLayerId = doc.Layers[parentIndex].Id;
                        doc.Layers.Modify(layer, layerIndex, true);
                    }
                }
                return layerIndex;
            }

            // Create a new layer
            var newLayer = new Layer
            {
                Name = layerName,
                Color = System.Drawing.Color.Black
            };

            if (parentIndex >= 0)
            {
                newLayer.ParentLayerId = doc.Layers[parentIndex].Id;
            }

            return doc.Layers.Add(newLayer);
        }
    }

    // Extended layer structure to handle floor types and level elevations
    public class LayerStructure
    {
        public int SetupLayerIndex { get; set; }
        public int BackgroundsLayerIndex { get; set; }
        public int GridsLayerIndex { get; set; }
        public Dictionary<string, int> FloorplateLayers { get; set; } // Maps floor type ID to layer index
        public Dictionary<string, int> FloorTypeLayers { get; set; } // Maps floor type ID to layer index
        public Dictionary<string, double> LevelElevations { get; set; } // Maps level ID to elevation
    }
}
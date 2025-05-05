using Rhino;
using Rhino.Commands;
using Rhino.UI;
using Rhino.Geometry;
using Rhino.DocObjects;
using System;
using System.IO;
using System.Linq;
using Rhino.Input;
using System.Collections.Generic;

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
            // Step 1: Prompt for directory selection using dialog
            string rootDirectory = SelectDirectory();
            if (string.IsNullOrEmpty(rootDirectory))
                return Result.Cancel;

            // Find DWG file
            string dwgFile = FindFirstDwgFile(rootDirectory);

            // Ask about CAD import if file exists
            bool importCad = false;
            if (!string.IsNullOrEmpty(dwgFile))
            {
                string fileName = Path.GetFileName(dwgFile);
                Result cadResult = RhinoGet.GetBool($"Import CAD file: {fileName}?", true, "No", "Yes", ref importCad);
                if (cadResult != Result.Success)
                    return cadResult;
            }

            // Step 3: Ask for number of floorplates
            int floorplateCount = 2; // Default
            Result countResult = RhinoGet.GetInteger("Number of floorplates", false, ref floorplateCount, 1, 20);
            if (countResult != Result.Success)
                return countResult;


            // Step 5: Begin setup command
            RhinoApp.RunScript("_-Command _Pause", false);

            try
            {
                // Create layer structure
                var setupLayer = CreateLayerStructure(doc, floorplateCount);

                // Create dummy geometry
                CreateDummyGeometry(doc, setupLayer, floorplateCount);

                // Import CAD file if requested
                if (importCad && !string.IsNullOrEmpty(dwgFile))
                {
                    ImportDwgFile(doc, dwgFile, setupLayer);
                }

                RhinoApp.RunScript("_-Command _Resume", false);
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("Error during setup: {0}", ex.Message);
                Rhino.RhinoApp.RunScript("_-Command _Cancel", false);
                return Result.Failure;
            }
        }

        string SelectDirectory()
        {
            using (var dialog = new Eto.Forms.SelectFolderDialog
            {
                Title = "Select Project Root Directory"
            })
            {
                var result = dialog.ShowDialog(RhinoEtoApp.MainWindow);
                if (result == Eto.Forms.DialogResult.Ok)
                    return dialog.Directory;
            }
            return null;
        }

        private LayerStructure CreateLayerStructure(RhinoDoc doc, int floorplateCount)
        {
            // Create the main setup layer
            int setupLayerIndex = GetOrCreateLayer(doc, "z-setup");

            var layers = new LayerStructure
            {
                SetupLayerIndex = setupLayerIndex,
                FloorplateLayers = new Dictionary<int, int>(),
                FloorsLayers = new Dictionary<int, int>()
            };

            // Create floorplate and floors layers
            for (int i = 1; i <= floorplateCount; i++)
            {
                string floorplateName = $"z-floorplate{i}";
                string floorsName = $"z-floors{i}";

                int floorplateIndex = GetOrCreateLayer(doc, floorplateName, setupLayerIndex);
                int floorsIndex = GetOrCreateLayer(doc, floorsName, floorplateIndex);

                layers.FloorplateLayers[i] = floorplateIndex;
                layers.FloorsLayers[i] = floorsIndex;
            }

            return layers;
        }

        private void CreateDummyGeometry(RhinoDoc doc, LayerStructure layers, int floorplateCount)
        {
            for (int i = 1; i <= floorplateCount; i++)
            {
                // Calculate elevation for this floorplate
                double elevation = (i - 1) * 100; // Start at 0, increment by 100

                // Create rectangle at the calculated elevation
                Point3d[] rectPoints = new Point3d[]
                {
            new Point3d(0, 0, elevation),
            new Point3d(100, 0, elevation),
            new Point3d(100, 100, elevation),
            new Point3d(0, 100, elevation),
            new Point3d(0, 0, elevation)
                };

                var polyline = new Polyline(rectPoints);
                var curve = polyline.ToNurbsCurve();

                var attr = new ObjectAttributes();
                attr.LayerIndex = layers.FloorsLayers[i];
                doc.Objects.AddCurve(curve, attr);
            }
        }

        private string FindFirstDwgFile(string rootDirectory)
        {
            // Check for CAD directory
            string cadDirectory = Path.Combine(rootDirectory, "CAD");

            if (Directory.Exists(cadDirectory))
            {
                // Get all DWG files and return the first one
                string[] dwgFiles = Directory.GetFiles(cadDirectory, "*.dwg");
                return dwgFiles.Length > 0 ? dwgFiles[0] : null;
            }

            return null;
        }

        private bool ImportDwgFile(RhinoDoc doc, string filePath, LayerStructure layers)
        {
            // Use the command line to import
            string cmd = $"-_Import \"{filePath}\" _Enter";
            return RhinoApp.RunScript(cmd, false);
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

    // Simple class to hold layer indices
    public class LayerStructure
    {
        public int SetupLayerIndex { get; set; }
        public Dictionary<int, int> FloorplateLayers { get; set; }
        public Dictionary<int, int> FloorsLayers { get; set; }
    }
}
using Rhino;
using Rhino.Commands;
using Rhino.UI.Dialogs;
using Rhino.Geometry;
using Rhino.DocObjects;
using System;
using System.IO;
using System.Linq;

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

            // Step 2: Check for CAD directory and get first DWG file
            string cadDirectory = Path.Combine(rootDirectory, "CAD");
            string dwgFile = null;

            if (Directory.Exists(cadDirectory))
            {
                dwgFile = Directory.GetFiles(cadDirectory, "*.dwg").FirstOrDefault();
            }

            // Step 3: Begin setup command
            Command.BeginCommand("Parametric Setup");

            try
            {
                // Create layer structure
                var setupLayer = CreateLayerStructure(doc);

                // Create dummy geometry
                CreateDummyGeometry(doc, setupLayer);

                // Import CAD file if found
                if (!string.IsNullOrEmpty(dwgFile))
                {
                    ImportDwgFile(doc, dwgFile, setupLayer);
                }

                Command.EndCommand();
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("Error during setup: {0}", ex.Message);
                Command.CancelCommand();
                return Result.Failure;
            }
        }

        private string SelectDirectory()
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Title = "Select Project Root Directory";
            dialog.FolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (dialog.ShowDialog())
                return dialog.FolderPath;

            return null;
        }

        private LayerStructure CreateLayerStructure(RhinoDoc doc)
        {
            // Create the main setup layer
            int setupLayerIndex = GetOrCreateLayer(doc, "z-setup");

            // Create first floorplate layer
            int floorplate1Index = GetOrCreateLayer(doc, "z-floorplate1", setupLayerIndex);
            int floors1Index = GetOrCreateLayer(doc, "z-floors", floorplate1Index);

            // Create second floorplate layer
            int floorplate2Index = GetOrCreateLayer(doc, "z-floorplate2", setupLayerIndex);
            int floors2Index = GetOrCreateLayer(doc, "z-floors", floorplate2Index);

            return new LayerStructure
            {
                SetupLayerIndex = setupLayerIndex,
                Floorplate1LayerIndex = floorplate1Index,
                Floors1LayerIndex = floors1Index,
                Floorplate2LayerIndex = floorplate2Index,
                Floors2LayerIndex = floors2Index
            };
        }

        private void CreateDummyGeometry(RhinoDoc doc, LayerStructure layers)
        {
            // Create rectangle at elevation 0 for first floorplate
            Point3d[] rect1Points = new Point3d[]
            {
                new Point3d(0, 0, 0),
                new Point3d(100, 0, 0),
                new Point3d(100, 100, 0),
                new Point3d(0, 100, 0),
                new Point3d(0, 0, 0)
            };

            var polyline1 = new Polyline(rect1Points);
            var curve1 = polyline1.ToNurbsCurve();

            var attr1 = new ObjectAttributes();
            attr1.LayerIndex = layers.Floors1LayerIndex;
            doc.Objects.AddCurve(curve1, attr1);

            // Create rectangle at elevation 100 inches for second floorplate
            Point3d[] rect2Points = new Point3d[]
            {
                new Point3d(0, 0, 100),
                new Point3d(100, 0, 100),
                new Point3d(100, 100, 100),
                new Point3d(0, 100, 100),
                new Point3d(0, 0, 100)
            };

            var polyline2 = new Polyline(rect2Points);
            var curve2 = polyline2.ToNurbsCurve();

            var attr2 = new ObjectAttributes();
            attr2.LayerIndex = layers.Floors2LayerIndex;
            doc.Objects.AddCurve(curve2, attr2);
        }

        private bool ImportDwgFile(RhinoDoc doc, string filePath, LayerStructure layers)
        {
            var options = new Rhino.FileIO.FileReadOptions
            {
                ImportMode = Rhino.FileIO.ImportMode.Insert,
                CurrentLayerIndex = layers.SetupLayerIndex
            };

            return doc.Import(filePath, options);
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
        public int Floorplate1LayerIndex { get; set; }
        public int Floors1LayerIndex { get; set; }
        public int Floorplate2LayerIndex { get; set; }
        public int Floors2LayerIndex { get; set; }
    }
}
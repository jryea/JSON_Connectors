using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.ModelLayout;
using Core.Models.Geometry;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;
using System.Diagnostics;

namespace RAM.Export.ModelLayout
{
    public class GridExport
    {
        private IModel _model;
        private string _lengthUnit;

        public GridExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public List<Grid> Export()
        {
            var grids = new List<Grid>();

            Debug.WriteLine($"Does IModel exist? Here is the version number: {_model.dVersion.ToString()} ");

            try
            {
                // Get grid systems from RAM
                IGridSystems gridSystems = _model.GetGridSystems();
                if (gridSystems == null || gridSystems.GetCount() == 0)
                    return grids;

                Debug.WriteLine($"Found {gridSystems.GetCount()} grid systems");

                // First Pass: Collect all grid coordinates from all grid systems
                var xCoordinates = new List<double>();
                var yCoordinates = new List<double>();

                for (int gsIndex = 0; gsIndex < gridSystems.GetCount(); gsIndex++)
                {
                    IGridSystem gridSystem = gridSystems.GetAt(gsIndex);
                    if (gridSystem == null)
                        continue;

                    IModelGrids modelGrids = gridSystem.GetGrids();
                    if (modelGrids == null || modelGrids.GetCount() == 0)
                        continue;

                    Debug.WriteLine($"Grid system {gsIndex} has {modelGrids.GetCount()} grids");

                    for (int i = 0; i < modelGrids.GetCount(); i++)
                    {
                        IModelGrid modelGrid = modelGrids.GetAt(i);
                        if (modelGrid == null)
                            continue;

                        double coordinate = modelGrid.dCoordinate_Angle;

                        if (modelGrid.eAxis == EGridAxis.eGridXorRadialAxis)
                        {
                            xCoordinates.Add(coordinate);
                            Debug.WriteLine($"  X Grid: {modelGrid.strLabel} at X={coordinate}");
                        }
                        else if (modelGrid.eAxis == EGridAxis.eGridYorCircularAxis)
                        {
                            yCoordinates.Add(coordinate);
                            Debug.WriteLine($"  Y Grid: {modelGrid.strLabel} at Y={coordinate}");
                        }
                    }
                }

                // Calculate extents (with fallback if no grids found in one direction)
                double minX = xCoordinates.Count > 0 ? xCoordinates.Min() : -100;
                double maxX = xCoordinates.Count > 0 ? xCoordinates.Max() : 100;
                double minY = yCoordinates.Count > 0 ? yCoordinates.Min() : -100;
                double maxY = yCoordinates.Count > 0 ? yCoordinates.Max() : 100;

                Debug.WriteLine($"Grid extents: X[{minX:F2}, {maxX:F2}], Y[{minY:F2}, {maxY:F2}]");

                // Second Pass: Create grids with proper endpoints
                for (int gsIndex = 0; gsIndex < gridSystems.GetCount(); gsIndex++)
                {
                    IGridSystem gridSystem = gridSystems.GetAt(gsIndex);
                    if (gridSystem == null)
                        continue;

                    IModelGrids modelGrids = gridSystem.GetGrids();
                    if (modelGrids == null || modelGrids.GetCount() == 0)
                        continue;

                    for (int i = 0; i < modelGrids.GetCount(); i++)
                    {
                        IModelGrid modelGrid = modelGrids.GetAt(i);
                        if (modelGrid == null)
                            continue;

                        // Convert coordinate from inches to target unit
                        double coordinate = ConvertFromInches(modelGrid.dCoordinate_Angle);
                        double convertedMinX = ConvertFromInches(minX);
                        double convertedMaxX = ConvertFromInches(maxX);
                        double convertedMinY = ConvertFromInches(minY);
                        double convertedMaxY = ConvertFromInches(maxY);

                        GridPoint startPoint, endPoint;

                        if (modelGrid.eAxis == EGridAxis.eGridXorRadialAxis)
                        {
                            // X grid (vertical line with constant X) - use Y extents for endpoints
                            startPoint = new GridPoint(coordinate, convertedMinY, 0, true);
                            endPoint = new GridPoint(coordinate, convertedMaxY, 0, true);
                            Debug.WriteLine($"Created X grid '{modelGrid.strLabel}' from ({coordinate:F2}, {convertedMinY:F2}) to ({coordinate:F2}, {convertedMaxY:F2})");
                        }
                        else
                        {
                            // Y grid (horizontal line with constant Y) - use X extents for endpoints
                            startPoint = new GridPoint(convertedMinX, coordinate, 0, true);
                            endPoint = new GridPoint(convertedMaxX, coordinate, 0, true);
                            Debug.WriteLine($"Created Y grid '{modelGrid.strLabel}' from ({convertedMinX:F2}, {coordinate:F2}) to ({convertedMaxX:F2}, {coordinate:F2})");
                        }

                        // Create grid
                        Grid grid = new Grid
                        {
                            Id = IdGenerator.Generate(IdGenerator.Layout.GRID),
                            Name = modelGrid.strLabel,
                            StartPoint = startPoint,
                            EndPoint = endPoint
                        };

                        grids.Add(grid);
                    }
                }

                Debug.WriteLine($"Total grids exported: {grids.Count}");
                return grids;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting grids from RAM: {ex.Message}");
                return grids;
            }
        }

        private double ConvertFromInches(double inches)
        {
            switch (_lengthUnit.ToLower())
            {
                case "feet":
                    return inches / 12.0;
                case "millimeters":
                    return inches * 25.4;
                case "centimeters":
                    return inches * 2.54;
                case "meters":
                    return inches * 0.0254;
                case "inches":
                default:
                    return inches;
            }
        }
    }
}
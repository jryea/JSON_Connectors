using System;
using System.Collections.Generic;
using Core.Models.ModelLayout;
using Core.Models.Geometry;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

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

            try
            {
                // Get grid systems from RAM
                IGridSystems gridSystems = _model.GetGridSystems();
                if (gridSystems == null || gridSystems.GetCount() == 0)
                    return grids;

                // Use the first grid system for simplicity
                // In a real implementation, you might want to handle multiple grid systems
                IGridSystem gridSystem = gridSystems.GetAt(0);
                if (gridSystem == null)
                    return grids;

                // Get grids from the system
                IModelGrids modelGrids = gridSystem.GetGrids();
                if (modelGrids == null || modelGrids.GetCount() == 0)
                    return grids;

                // Process each grid
                for (int i = 0; i < modelGrids.GetCount(); i++)
                {
                    IModelGrid modelGrid = modelGrids.GetAt(i);
                    if (modelGrid == null)
                        continue;

                    // Determine start and end points based on grid direction
                    double coordinate = ConvertFromInches(modelGrid.dCoordinate_Angle);
                    GridPoint startPoint, endPoint;

                    if (modelGrid.eAxis == EGridAxis.eGridXorRadialAxis)
                    {
                        // X grid (vertical line with constant X)
                        startPoint = new GridPoint(coordinate, -1000, 0, true);
                        endPoint = new GridPoint(coordinate, 1000, 0, true);
                    }
                    else
                    {
                        // Y grid (horizontal line with constant Y)
                        startPoint = new GridPoint(-1000, coordinate, 0, true);
                        endPoint = new GridPoint(1000, coordinate, 0, true);
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
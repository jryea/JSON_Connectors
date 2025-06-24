// GridImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.ModelLayout
{
    // Imports grid lines to RAM from the Core model
    public class GridImport
    {
        private IModel _model;
        private string _lengthUnit;
        private string _gridSystemName;

        public GridImport(IModel model, string lengthUnit = "inches", string gridSystemName = "StandardGrids")
        {
            _model = model;
            _lengthUnit = lengthUnit;
            _gridSystemName = gridSystemName;
        }

        // Imports grid lines to RAM
        public int Import(IEnumerable<Grid> grids)
        {
            try
            {
                int count = 0;

                // Get grid systems from model
                IGridSystems gridSystems = _model.GetGridSystems();
                IGridSystem gridSystem;

                // Create a new grid system or use existing one
                if (gridSystems.GetCount() > 0)
                {
                    gridSystem = gridSystems.GetAt(0);
                }
                else
                {
                    gridSystem = gridSystems.Add(_gridSystemName);
                }

                // Get grids from the grid system
                IModelGrids modelGrids = gridSystem.GetGrids();

                foreach (var grid in grids)
                {
                    if (grid.StartPoint == null || grid.EndPoint == null || string.IsNullOrEmpty(grid.Name))
                        continue;

                    // Convert coordinates to RAM units (inches)
                    double startX = UnitConversionUtils.ConvertToInches(grid.StartPoint.X, _lengthUnit);
                    double startY = UnitConversionUtils.ConvertToInches(grid.StartPoint.Y, _lengthUnit);
                    double endX = UnitConversionUtils.ConvertToInches(grid.EndPoint.X, _lengthUnit);
                    double endY = UnitConversionUtils.ConvertToInches(grid.EndPoint.Y, _lengthUnit);

                    // Determine grid type
                    bool isVertical = AreLinePointsVertical(grid.StartPoint, grid.EndPoint);
                    bool isHorizontal = AreLinePointsHorizontal(grid.StartPoint, grid.EndPoint);
                    bool isAngled = !isVertical && !isHorizontal;

                    IModelGrid modelGrid;

                    if (isAngled)
                    {
                        // Calculate angle for angled grid
                        double deltaX = endX - startX;
                        double deltaY = endY - startY;
                        double angleRadians = Math.Atan2(deltaY, deltaX);
                        double angleDegrees = angleRadians * (180.0 / Math.PI);

                        // Add angled grid using radial axis
                        modelGrid = modelGrids.Add(grid.Name, EGridAxis.eGridXorRadialAxis, angleDegrees);
                        count++;
                    }
                    else if (isVertical)
                    {
                        // Add vertical grid
                        modelGrid = modelGrids.Add(grid.Name, EGridAxis.eGridXorRadialAxis, startX);
                        count++;
                    }
                    else
                    {
                        // Add horizontal grid
                        modelGrid = modelGrids.Add(grid.Name, EGridAxis.eGridYorCircularAxis, startY);
                        count++;
                    }
                }

                // Apply grid system to all floor types
                ApplyGridSystemToFloorTypes(gridSystem);

                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing grids: {ex.Message}");
                throw;
            }
        }

        // Helper method to check if two points form a vertical line
        private bool AreLinePointsVertical(GridPoint startPoint, GridPoint endPoint)
        {
            return Math.Abs(startPoint.X - endPoint.X) < 1e-6;
        }

        // Helper method to check if two points form a horizontal line
        private bool AreLinePointsHorizontal(GridPoint startPoint, GridPoint endPoint)
        {
            return Math.Abs(startPoint.Y - endPoint.Y) < 1e-6;
        }

        // Applies the grid system to all floor types in the model
        private void ApplyGridSystemToFloorTypes(IGridSystem gridSystem)
        {
            try
            {
                IFloorTypes floorTypes = _model.GetFloorTypes();

                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType floorType = floorTypes.GetAt(i);
                    DAArray gridSystemArray = floorType.GetGridSystemIDArray();
                    int gridArray = 0;
                    gridSystemArray.Add(gridSystem.lUID, ref gridArray);
                    floorType.SetGridSystemIDArray(gridSystemArray);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying grid system to floor types: {ex.Message}");
                // Continue execution, this is not critical
            }
        }
    }
}
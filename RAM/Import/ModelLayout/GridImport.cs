// GridImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
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

                    // Determine if grid is horizontal or vertical
                    bool isVertical = Core.Utilities.Utilities.AreLinePointsVertical(grid.StartPoint, grid.EndPoint);

                    // Convert coordinates to RAM units (inches)
                    double startX = Helpers.ConvertToInches(grid.StartPoint.X, _lengthUnit);
                    double startY = Helpers.ConvertToInches(grid.StartPoint.Y, _lengthUnit);
                    double endX = Helpers.ConvertToInches(grid.EndPoint.X, _lengthUnit);
                    double endY = Helpers.ConvertToInches(grid.EndPoint.Y, _lengthUnit);

                    // For vertical grids, the X coordinate is constant
                    if (isVertical)
                    {
                        // Add vertical grid
                        IModelGrid modelGrid = modelGrids.Add(grid.Name, EGridAxis.eGridXorRadialAxis, startX);
                        count++;
                    }
                    else
                    {
                        // Add horizontal grid
                        IModelGrid modelGrid = modelGrids.Add(grid.Name, EGridAxis.eGridYorCircularAxis, startY);
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
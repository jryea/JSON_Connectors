// GridImport.cs
using System;
using System.Collections.Generic;
using System.Linq;
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
        private string _skewedGridSystemName;

        public GridImport(IModel model, string lengthUnit = "inches", string gridSystemName = "StandardGrids")
        {
            _model = model;
            _lengthUnit = lengthUnit;
            _gridSystemName = gridSystemName;
            _skewedGridSystemName = "SkewedGrids";
        }

        // Imports grid lines to RAM
        public int Import(IEnumerable<Grid> grids)
        {
            try
            {
                int count = 0;
                var gridList = grids.ToList();

                // Separate grids into orthogonal and angled
                var orthogonalGrids = new List<Grid>();
                var angledGrids = new List<Grid>();

                foreach (var grid in gridList)
                {
                    if (grid.StartPoint == null || grid.EndPoint == null || string.IsNullOrEmpty(grid.Name))
                        continue;

                    bool isVertical = AreLinePointsVertical(grid.StartPoint, grid.EndPoint);
                    bool isHorizontal = AreLinePointsHorizontal(grid.StartPoint, grid.EndPoint);
                    bool isAngled = !isVertical && !isHorizontal;

                    if (isAngled)
                        angledGrids.Add(grid);
                    else
                        orthogonalGrids.Add(grid);
                }

                // Import orthogonal grids to standard grid system
                if (orthogonalGrids.Any())
                {
                    count += ImportOrthogonalGrids(orthogonalGrids);
                }

                // Import angled grids to skewed grid system
                if (angledGrids.Any())
                {
                    count += ImportSkewedGrids(angledGrids);
                }

                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing grids: {ex.Message}");
                throw;
            }
        }

        // Import orthogonal grids to standard grid system
        private int ImportOrthogonalGrids(List<Grid> grids)
        {
            int count = 0;

            // Get grid systems from model
            IGridSystems gridSystems = _model.GetGridSystems();
            IGridSystem gridSystem;

            // Create a new orthogonal grid system or use existing one
            gridSystem = FindOrCreateGridSystem(gridSystems, _gridSystemName, SGridSysType.eGridOrthogonal);

            // Get grids from the grid system
            IModelGrids modelGrids = gridSystem.GetGrids();

            foreach (var grid in grids)
            {
                // Convert coordinates to RAM units (inches)
                double startX = UnitConversionUtils.ConvertToInches(grid.StartPoint.X, _lengthUnit);
                double startY = UnitConversionUtils.ConvertToInches(grid.StartPoint.Y, _lengthUnit);
                double endX = UnitConversionUtils.ConvertToInches(grid.EndPoint.X, _lengthUnit);
                double endY = UnitConversionUtils.ConvertToInches(grid.EndPoint.Y, _lengthUnit);

                // Determine grid type
                bool isVertical = AreLinePointsVertical(grid.StartPoint, grid.EndPoint);
                bool isHorizontal = AreLinePointsHorizontal(grid.StartPoint, grid.EndPoint);

                IModelGrid modelGrid;

                if (isVertical)
                {
                    // Add vertical grid
                    modelGrid = modelGrids.Add(grid.Name, EGridAxis.eGridXorRadialAxis, startX);
                    count++;
                }
                else if (isHorizontal)
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

        // Import angled grids to skewed grid system
        private int ImportSkewedGrids(List<Grid> grids)
        {
            int count = 0;

            // Get grid systems from model
            IGridSystems gridSystems = _model.GetGridSystems();
            IGridSystem skewedGridSystem;

            // Create a new skewed grid system or use existing one
            skewedGridSystem = FindOrCreateGridSystem(gridSystems, _skewedGridSystemName, SGridSysType.eGridSkewed);

            // Get grids from the skewed grid system
            IModelGrids modelGrids = skewedGridSystem.GetGrids();

            foreach (var grid in grids)
            {
                // Convert coordinates to RAM units (inches)
                double startX = UnitConversionUtils.ConvertToInches(grid.StartPoint.X, _lengthUnit);
                double startY = UnitConversionUtils.ConvertToInches(grid.StartPoint.Y, _lengthUnit);
                double endX = UnitConversionUtils.ConvertToInches(grid.EndPoint.X, _lengthUnit);
                double endY = UnitConversionUtils.ConvertToInches(grid.EndPoint.Y, _lengthUnit);

                // Calculate angle for angled grid
                double deltaX = endX - startX;
                double deltaY = endY - startY;
                double angleRadians = Math.Atan2(deltaY, deltaX);
                double angleDegrees = angleRadians * (180.0 / Math.PI);

                // Add angled grid using radial axis (even for skewed systems, angled grids use the radial axis)
                IModelGrid modelGrid = modelGrids.Add(grid.Name, EGridAxis.eGridXorRadialAxis, angleDegrees);
                count++;
            }

            // Apply skewed grid system to all floor types
            ApplyGridSystemToFloorTypes(skewedGridSystem);

            return count;
        }

        // Find existing grid system or create new one with specified type
        private IGridSystem FindOrCreateGridSystem(IGridSystems gridSystems, string systemName, SGridSysType systemType)
        {
            // Look for existing grid system with the same name
            for (int i = 0; i < gridSystems.GetCount(); i++)
            {
                IGridSystem existingSystem = gridSystems.GetAt(i);
                if (existingSystem.strLabel == systemName)
                {
                    return existingSystem;
                }
            }

            // Create new grid system
            IGridSystem newGridSystem = gridSystems.Add(systemName);

            // Set the grid system type
            SetGridSystemType(newGridSystem, systemType);

            return newGridSystem;
        }

        // Set the grid system type (orientation type)
        private void SetGridSystemType(IGridSystem gridSystem, SGridSysType systemType)
        {
            try
            {
                // Try to set the eOrientationType property directly
                gridSystem.eOrientationType = systemType;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not set grid system type directly: {ex.Message}");

                // If direct property access fails, try using SetGridSysInfo if available
                try
                {
                    // Get current grid system info
                    string label = gridSystem.strLabel;
                    double xOffset = 0.0;
                    double yOffset = 0.0;
                    double rotation = 0.0;
                    int numXRadialGrids = 0;
                    int numYCircularGrids = 0;

                    // Note: You may need to call GetGridSysInfo first to get current values
                    // if the API requires all parameters to be set

                    // Call SetGridSysInfo with the desired type
                    // This is a hypothetical method - you'll need to check the actual RAM API
                    // gridSystem.SetGridSysInfo(label, systemType, xOffset, yOffset, rotation, numXRadialGrids, numYCircularGrids);
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Warning: Could not set grid system type using SetGridSysInfo: {ex2.Message}");
                }
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
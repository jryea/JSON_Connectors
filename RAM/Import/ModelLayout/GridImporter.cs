// GridImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class GridImporter : IRAMImporter<List<Grid>>
    {
        private IModel _model;

        public GridImporter(IModel model)
        {
            _model = model;
        }

        public List<Grid> Import()
        {
            var grids = new List<Grid>();

            try
            {
                // Get grid systems from RAM
                IGridSystems gridSystems = _model.GetGridSystems();

                if (gridSystems.GetCount() > 0)
                {
                    // Use the first grid system (RAM typically has one grid system)
                    IGridSystem gridSystem = gridSystems.GetAt(0);
                    IModelGrids modelGrids = gridSystem.GetGrids();

                    for (int i = 0; i < modelGrids.GetCount(); i++)
                    {
                        IModelGrid modelGrid = modelGrids.GetAt(i);

                        // Create grid points
                        GridPoint startPoint, endPoint;

                        // Determine if this is an X or Y direction grid
                        if (modelGrid.eDirection == EGridAxis.eGridXorRadialAxis)
                        {
                            // X axis grid (vertical line with constant X coordinate)
                            double xCoord = modelGrid.dLocation / 12.0; // Convert to feet
                            startPoint = new GridPoint(xCoord, -500, 0, true); // Bottom point
                            endPoint = new GridPoint(xCoord, 500, 0, true); // Top point
                        }
                        else
                        {
                            // Y axis grid (horizontal line with constant Y coordinate)
                            double yCoord = modelGrid.dLocation / 12.0; // Convert to feet
                            startPoint = new GridPoint(-500, yCoord, 0, true); // Left point
                            endPoint = new GridPoint(500, yCoord, 0, true); // Right point
                        }

                        // Create grid
                        var grid = new Grid
                        {
                            Id = IdGenerator.Generate(IdGenerator.Layout.GRID),
                            Name = modelGrid.strLabel,
                            StartPoint = startPoint,
                            EndPoint = endPoint
                        };

                        grids.Add(grid);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing grids: {ex.Message}");
            }

            return grids;
        }
    }
}
// GridExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.ModelLayout;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class GridExporter : IRAMExporter
    {
        private IModel _model;

        public GridExporter(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Get existing grid systems or create a new one
            IGridSystems gridSystems = _model.GetGridSystems();
            IGridSystem gridSystem;

            if (gridSystems.GetCount() > 0)
            {
                gridSystem = gridSystems.GetAt(0);
            }
            else
            {
                gridSystem = gridSystems.Add("SpeedGrids");
            }

            IModelGrids grids = gridSystem.GetGrids();

            // Group grids by direction (X and Y)
            var xGrids = model.ModelLayout.Grids.Where(g => Math.Abs(g.StartPoint.X - g.EndPoint.X) < 0.001).ToList();
            var yGrids = model.ModelLayout.Grids.Where(g => Math.Abs(g.StartPoint.Y - g.EndPoint.Y) < 0.001).ToList();

            // Add X grids
            foreach (var grid in xGrids)
            {
                try
                {
                    double gridCoord = grid.StartPoint.X * 12; // Convert to inches
                    grids.Add(grid.Name, EGridAxis.eGridXorRadialAxis, gridCoord);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting X grid {grid.Name}: {ex.Message}");
                }
            }

            // Add Y grids
            foreach (var grid in yGrids)
            {
                try
                {
                    double gridCoord = grid.StartPoint.Y * 12; // Convert to inches
                    grids.Add(grid.Name, EGridAxis.eGridYorCircularAxis, gridCoord);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting Y grid {grid.Name}: {ex.Message}");
                }
            }

            // Add grid system to floor types
            foreach (var floorType in model.ModelLayout.FloorTypes)
            {
                IFloorTypes floorTypes = _model.GetFloorTypes();

                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType curFloorType = floorTypes.GetAt(i);

                    if (curFloorType.strLabel == floorType.Name)
                    {
                        DAArray gridSystemArray = curFloorType.GetGridSystemIDArray();
                        int gridArray = 0;
                        gridSystemArray.Add(gridSystem.lUID, ref gridArray);
                        curFloorType.SetGridSystemIDArray(gridSystemArray);
                    }
                }
            }
        }
    }
}
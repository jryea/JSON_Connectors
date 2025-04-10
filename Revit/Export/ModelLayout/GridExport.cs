using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CG = Core.Models.Geometry;
using Core.Models.ModelLayout;
using Revit.Utilities;

namespace Revit.Export.ModelLayout
{
    public class GridExport
    {
        private readonly DB.Document _doc;

        public GridExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<Grid> grids)
        {
            int count = 0;

            // Get all grids from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Grid> revitGrids = collector.OfClass(typeof(DB.Grid))
                .Cast<DB.Grid>()
                .ToList();

            foreach (var revitGrid in revitGrids)
            {
                try
                {
                    DB.Curve curve = revitGrid.Curve;
                    if (curve is DB.Line line)
                    {
                        // Get grid start and end points
                        DB.XYZ start = line.GetEndPoint(0);
                        DB.XYZ end = line.GetEndPoint(1);

                        // Convert to Grid points
                        CG.GridPoint startPoint = new CG.GridPoint(
                            start.X * 12.0,  // Convert feet to inches
                            start.Y * 12.0,
                            start.Z * 12.0,
                            true  // Assume bubble is visible at start
                        );

                        CG.GridPoint endPoint = new CG.GridPoint(
                            end.X * 12.0,
                            end.Y * 12.0,
                            end.Z * 12.0,
                            true  // Assume bubble is visible at end
                        );

                        // Check bubble visibility (if possible)
                        DB.View activeView = _doc.ActiveView;
                        if (activeView != null)
                        {
                            try
                            {
                                startPoint.IsBubble = revitGrid.IsBubbleVisibleInView(DB.DatumEnds.End0, activeView);
                                endPoint.IsBubble = revitGrid.IsBubbleVisibleInView(DB.DatumEnds.End1, activeView);
                            }
                            catch
                            {
                                // Default to true if visibility cannot be determined
                            }
                        }

                        // Create grid object
                        Grid grid = new Grid
                        {
                            Name = revitGrid.Name,
                            StartPoint = startPoint,
                            EndPoint = endPoint
                        };

                        grids.Add(grid);
                        count++;
                    }
                }
                catch (Exception)
                {
                    // Skip this grid and continue with the next one
                }
            }

            return count;
        }
    }
}
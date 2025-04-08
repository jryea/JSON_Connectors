using System;
using System.Collections.Generic;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using Core.Models.ModelLayout;
using Revit.Utilities;

namespace Revit.Import.ModelLayout
{
    // Imports grid elements from JSON into Revit
    public class GridImport
    {
        private readonly DB.Document _doc;

        public GridImport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Import(List<Grid> grids)
        {
            int count = 0;

            foreach (var jsonGrid in grids)
            {
                try
                {
                    // Convert JSON grid points to Revit XYZ
                    DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonGrid.StartPoint);
                    DB.XYZ endPoint = Helpers.ConvertToRevitCoordinates(jsonGrid.EndPoint);

                    // Create line for grid
                    DB.Line gridLine = DB.Line.CreateBound(startPoint, endPoint);

                    // Create grid in Revit
                    DB.Grid revitGrid = DB.Grid.Create(_doc, gridLine);

                    // Set grid name
                    revitGrid.Name = jsonGrid.Name;

                    // Apply bubble visibility if specified in JSON
                    if (jsonGrid.StartPoint.IsBubble)
                    {
                        revitGrid.ShowBubbleInView(DB.DatumEnds.End0, _doc.ActiveView);
                    }
                    else
                    {
                        revitGrid.HideBubbleInView(DB.DatumEnds.End0, _doc.ActiveView);
                    }

                    if (jsonGrid.EndPoint.IsBubble)
                    {
                        revitGrid.ShowBubbleInView(DB.DatumEnds.End1, _doc.ActiveView);
                    }
                    else
                    {
                        revitGrid.HideBubbleInView(DB.DatumEnds.End1, _doc.ActiveView);
                    }
                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this grid but continue with the next one
                    Debug.WriteLine($"Error creating grid {jsonGrid.Name}: {ex.Message}");
                }
            }

            return count;
        }
    }
}